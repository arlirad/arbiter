using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Channels;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;
using Arbiter.Core.Enums;
using Serilog;

namespace Arbiter.Transport.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class QuicAcceptor(ICertificateManager certificateManager, QuicPortService quicPortService) : IAcceptor, IAsyncConfigurable<List<IPAddress>, QuicTransportConfig, HashSet<Protocol>>, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "quic");
    private readonly ICertificateManager _certificateManager = certificateManager;
    private readonly QuicPortService _quicPortService = quicPortService;
    private readonly ConcurrentDictionary<IPEndPoint, QuicAcceptorListener> _listeners = new();
    private Channel<ITransport>? _transports;

    public async Task<ITransport> Accept(CancellationToken ct) => await _transports!.Reader.ReadAsync(ct);

    public async ValueTask ReconfigureAsync(List<IPAddress> addresses, QuicTransportConfig config, HashSet<Protocol> protocols)
    {
        if (!protocols.Contains(Protocol.Http3))
            return;

        if (config.Ports is null || config.Ports.Count == 0)
            Log.Warning("No ports configured");

        _transports ??= Channel.CreateBounded<ITransport>(new BoundedChannelOptions(config.QueueSize));
        _quicPortService.Ports = config.Ports;

        if (_quicPortService.Announce != config.Announce)
        {
            _quicPortService.Announce = config.Announce;
            Log.Information("Alt-Svc announce {State}", config.Announce ? "enabled" : "disabled");
        }

        var endpoints = new List<IPEndPoint>();
        foreach (var address in addresses)
        {
            foreach (var port in config.Ports)
                endpoints.Add(new IPEndPoint(address, port));
        }

        await Bind(endpoints, config.Backlog, config.MaxInboundBiStreams);
    }

    public async Task Bind(IEnumerable<IPEndPoint> endpoints, int backlog, int maxInboundBiStreams)
    {
        _transports ??= Channel.CreateBounded<ITransport>(new BoundedChannelOptions(4096));
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

    private async Task ConnectionLoop(QuicConnection quicConnection, CancellationToken ct)
    {
        try
        {
            var port = quicConnection.LocalEndPoint.Port;
            var remoteAddress = quicConnection.RemoteEndPoint?.Address;

            var transport = new QuicTransport(quicConnection, port, remoteAddress);

            await _transports.Writer.WriteAsync(transport, ct);
        }
        catch (OperationCanceledException)
        {
            // ignored
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

            var listener = await QuicListener.ListenAsync(new QuicListenerOptions() {
                ApplicationProtocols = [SslApplicationProtocol.Http3],
                ListenBacklog = backlog,
                ListenEndPoint = endpoint,
                ConnectionOptionsCallback = (_, clientHello, _) => ConnectionOptionsCallback(clientHello, maxInboundBiStreams),
            });

            var acceptorSocket = new QuicAcceptorListener(listener);

            _listeners[endpoint] = acceptorSocket;
            _ = AcceptLoop(listener, acceptorSocket.CancellationToken);
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
        var cert = _certificateManager.Get(clientHello.ServerName) ?? _certificateManager.GetFallback();
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

    public void Dispose()
    {
        foreach (var listener in _listeners.Values)
        {
            listener.Stop();
            listener.Close();
        }

        _listeners.Clear();
    }
}
