using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Threading.Channels;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arlirad.Http3;
using Arlirad.Http3.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Serilog;

namespace Arbiter.Transport.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class QuicAcceptor(
    ICertificateManager certificateManager
) : IAcceptor, IAsyncConfigurable
{
    private const int Backlog = 128;

    private readonly ConcurrentDictionary<IPEndPoint, QuicAcceptorListener> _listeners = new();

    private readonly Channel<ITransaction> _transactions =
        Channel.CreateBounded<ITransaction>(new BoundedChannelOptions(4096));

    private ConfigurationScope? _scope;

    public async Task<ITransaction> Accept(CancellationToken ct)
    {
        while (true)
            return await _transactions.Reader.ReadAsync(ct);
    }

    public async ValueTask Bind(IConfiguration configuration)
    {
        _scope = new ConfigurationScope(configuration, "ListenOn", "QuicPorts");
        await UpdateEndpoints();
        ChangeToken.OnChange(_scope.GetReloadToken, () => _ = UpdateEndpointsAsync());
    }

    private async Task UpdateEndpointsAsync()
    {
        try
        {
            await UpdateEndpoints();
        }
        catch (Exception e)
        {
            Log.Error("Failed to reload config: {Exception}", e);
        }
    }

    public async Task Bind(IEnumerable<IPEndPoint> endpoints)
    {
        await CreateListeners(endpoints);
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
                    // ignored
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }

    private async Task ConnectionLoop(QuicConnection quicConnection, CancellationToken ct)
    {
        try
        {
            var port = quicConnection.LocalEndPoint.Port;
            var remoteAddress = (quicConnection.RemoteEndPoint as IPEndPoint)?.Address;

            await using var transport = new Http3Transport(quicConnection, port, remoteAddress);

            await foreach (var transaction in transport.AcceptTransactions(ct))
                await _transactions.Writer.WriteAsync(transaction, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await quicConnection.CloseAsync((long)ErrorCode.InternalError, CancellationToken.None);
        }
    }

    private async Task CreateListeners(IEnumerable<IPEndPoint> endpoints)
    {
        foreach (var endpoint in endpoints.Where(e => !_listeners.ContainsKey(e)))
        {
            var listener = await QuicListener.ListenAsync(new QuicListenerOptions() {
                ApplicationProtocols = [SslApplicationProtocol.Http3],
                ListenBacklog = Backlog,
                ListenEndPoint = endpoint,
                ConnectionOptionsCallback = ConnectionOptionsCallback,
            });

            var acceptorSocket = new QuicAcceptorListener(listener);

            _listeners[endpoint] = acceptorSocket;
            _ = AcceptLoop(listener, acceptorSocket.CancellationToken);
        }
    }

    private async Task PruneListeners(IEnumerable<IPEndPoint> endpoints)
    {
        var toRemove = _listeners.Keys.Where(e => !endpoints.Contains(e)).ToList();
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
        QuicConnection connection,
        SslClientHelloInfo clientHello,
        CancellationToken ct)
    {
        var cert = certificateManager.Get(clientHello.ServerName) ?? certificateManager.GetFallback();
        var options = new QuicServerConnectionOptions {
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 1,
            ServerAuthenticationOptions = new SslServerAuthenticationOptions {
                ClientCertificateRequired = false,
                ServerCertificate = cert,
                EnabledSslProtocols = SslProtocols.Tls13,
                ApplicationProtocols = [SslApplicationProtocol.Http3],
            },
        };

        return ValueTask.FromResult(options);
    }

    private async Task UpdateEndpoints()
    {
        try
        {
            if (_scope is null)
                return;

            var endpoints = ExtractConfigBindings();

            if (endpoints.Any())
                await Bind(endpoints);
        }
        catch (Exception e)
        {
            Log.Error("Failed to reload config: {Exception}", e);
        }
    }

    private IEnumerable<IPEndPoint> ExtractConfigBindings()
    {
        if (_scope is null)
            return [];

        var listenOn = _scope.GetSection("ListenOn").Get<List<string>>();
        var quicPorts = _scope.GetSection("QuicPorts").Get<List<int>>();

        if (listenOn is null || quicPorts is null)
            return [];

        var endpoints = new List<IPEndPoint>();

        foreach (var address in listenOn)
        {
            foreach (var port in quicPorts)
                endpoints.Add(new IPEndPoint(IPAddress.Parse(address), port));
        }

        return endpoints;
    }
}