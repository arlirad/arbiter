using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Threading.Channels;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Services;
using Arbiter.Configuration;
using Arbiter.Core.Enums;
using Arbiter.Transport.Quic.Configuration;
using Serilog;

namespace Arbiter.Transport.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class QuicTransport(ICertificateManager certificateManager, AltSvcService altSvcService) : ITransport, IAsyncConfigurable<List<IPAddress>, QuicTransportConfig, HashSet<Protocol>>, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "quic");
    private readonly ConcurrentDictionary<IPEndPoint, QuicTransportListener> _listeners = new();
    private Channel<IConnection>? _connections;

    public async Task<IConnection> Accept(CancellationToken ct) => await _connections!.Reader.ReadAsync(ct);

    public async ValueTask ReconfigureAsync(List<IPAddress> addresses, QuicTransportConfig config, HashSet<Protocol> protocols)
    {
        if (!protocols.Contains(Protocol.Http3))
            return;

        if (config.Ports is null || config.Ports.Count == 0)
            Log.Warning("No ports configured");

        _connections ??= Channel.CreateBounded<IConnection>(new BoundedChannelOptions(config.QueueSize));

        if (config.Announce is not null)
        {
            var port = (config.Ports ?? []).OrderBy(p => p).FirstOrDefault();
            if (port != 0)
                altSvcService.Set("h3", $":{port}", config.Announce.MaxAge);
        }
        else
        {
            altSvcService.Remove("h3");
        }

        var endpoints = new List<IPEndPoint>();

        foreach (var address in addresses)
        {
            foreach (var port in config.Ports ?? [])
                endpoints.Add(new IPEndPoint(address, port));
        }

        await Bind(endpoints, config.Backlog, config.MaxInboundBiStreams);
    }

    public void Dispose()
    {
        foreach (var listener in _listeners.Values)
        {
            _ = listener.Stop();
            _ = listener.Close();
        }

        _listeners.Clear();
    }

    public async Task Bind(IEnumerable<IPEndPoint> endpoints, int backlog, int maxInboundBiStreams)
    {
        _connections ??= Channel.CreateBounded<IConnection>(new BoundedChannelOptions(4096));
        await CreateListeners(endpoints, backlog, maxInboundBiStreams);
        await PruneListeners(endpoints);
    }

    private async Task AcceptLoop(QuicListener listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var connection = await listener.AcceptConnectionAsync(ct);
                    _ = ConnectionLoop(connection, ct);
                }
                catch (QuicException)
                {
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ConnectionLoop(System.Net.Quic.QuicConnection quicConnection, CancellationToken ct)
    {
        try
        {
            var port = quicConnection.LocalEndPoint.Port;
            var remoteAddress = quicConnection.RemoteEndPoint?.Address;

            var connection = new QuicConnection(quicConnection, port, remoteAddress);

            await _connections!.Writer.WriteAsync(connection, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await quicConnection.CloseAsync(1, CancellationToken.None);
        }
    }

    private async Task CreateListeners(IEnumerable<IPEndPoint> endpoints, int backlog, int maxInboundBiStreams)
    {
        var newEndpoints = endpoints.Where(e => !_listeners.ContainsKey(e)).ToList();

        if (newEndpoints.Count > 0)
            Log.Information("Binding {Count} endpoint(s): {Endpoints}", newEndpoints.Count, newEndpoints);

        foreach (var endpoint in newEndpoints)
        {
            var listener = await QuicListener.ListenAsync(new QuicListenerOptions {
                ApplicationProtocols = [SslApplicationProtocol.Http3],
                ListenBacklog = backlog,
                ListenEndPoint = endpoint,
                ConnectionOptionsCallback = (_, clientHello, _) => ConnectionOptionsCallback(clientHello, maxInboundBiStreams),
            });

            var transportListener = new QuicTransportListener(listener);

            _listeners[endpoint] = transportListener;
            _ = AcceptLoop(listener, transportListener.CancellationToken);
        }
    }

    private async Task PruneListeners(IEnumerable<IPEndPoint> endpoints)
    {
        var toRemove = _listeners.Keys.Where(e => !endpoints.Contains(e)).ToList();

        if (toRemove.Count > 0)
            Log.Information("Pruning {Count} endpoint(s): {Endpoints}", toRemove.Count, toRemove);

        var cancellationTasks = new List<Task>();

        foreach (var endpoint in toRemove)
        {
            if (!_listeners.TryRemove(endpoint, out var listener))
                continue;

            cancellationTasks.Add(listener.Stop());
            cancellationTasks.Add(listener.Close());
        }

        if (cancellationTasks.Count > 0)
            await Task.WhenAll(cancellationTasks);
    }

    private ValueTask<QuicServerConnectionOptions> ConnectionOptionsCallback(
        SslClientHelloInfo clientHello,
        int maxInboundBiStreams)
    {
        var cert = certificateManager.Get(clientHello.ServerName) ?? certificateManager.GetFallback();
        var options = new QuicServerConnectionOptions {
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 1,
            MaxInboundBidirectionalStreams = maxInboundBiStreams,
            MaxInboundUnidirectionalStreams = 128,
            ServerAuthenticationOptions = new SslServerAuthenticationOptions {
                ClientCertificateRequired = false,
                ServerCertificate = cert,
                EnabledSslProtocols = SslProtocols.Tls13,
                ApplicationProtocols = [SslApplicationProtocol.Http3],
            },
        };

        return ValueTask.FromResult(options);
    }
}
