using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Channels;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;
using Arbiter.Transport.Quic.Models;
using Serilog;

namespace Arbiter.Transport.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class QuicAcceptor : IAcceptor, IAsyncConfigurable<QuicListenConfig>, IDisposable
{
    private const int Backlog = 128;
    private const int MaxInboundBidirectionalStreams = 1024;
    private const int MaxInboundUnidirectionalStreams = 128;

    private readonly ICertificateManager _certificateManager;
    private readonly ConcurrentDictionary<IPEndPoint, QuicAcceptorListener> _listeners = new();
    private readonly Channel<ITransport> _transports =
        Channel.CreateBounded<ITransport>(new BoundedChannelOptions(4096));
    private readonly CompositeDisposable _subscriptions = [];
    private readonly SemaphoreSlim _reconfigureLock = new(1, 1);

    public QuicAcceptor(ICertificateManager certificateManager, ConfigurationProvider configProvider)
    {
        _certificateManager = certificateManager;

        var quicConfig = Observable.CombineLatest(
            configProvider.Observe<List<string>>("ListenOn"),
            configProvider.Observe<List<int>>("QuicPorts"),
            (listenOn, quicPorts) => {
                var addresses = listenOn.Select(IPAddress.Parse).ToList();
                return new QuicListenConfig(addresses, quicPorts);
            }
        );

        _subscriptions.Add(quicConfig.Subscribe(async config => {
            try
            {
                await _reconfigureLock.WaitAsync();
                try
                {
                    await ReconfigureAsync(config);
                }
                finally
                {
                    _reconfigureLock.Release();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reconfigure QUIC acceptor");
            }
        }));
    }

    public async Task<ITransport> Accept(CancellationToken ct)
    {
        while (true)
            return await _transports.Reader.ReadAsync(ct);
    }

    public async ValueTask ReconfigureAsync(QuicListenConfig config)
    {
        var endpoints = new List<IPEndPoint>();
        foreach (var address in config.Addresses)
        {
            foreach (var port in config.Ports)
                endpoints.Add(new IPEndPoint(address, port));
        }

        await Bind(endpoints);
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
        var cert = _certificateManager.Get(clientHello.ServerName) ?? _certificateManager.GetFallback();
        var options = new QuicServerConnectionOptions {
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 1,
            MaxInboundBidirectionalStreams = MaxInboundBidirectionalStreams,
            MaxInboundUnidirectionalStreams = MaxInboundUnidirectionalStreams,
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
        _subscriptions.Dispose();
        _reconfigureLock.Dispose();
    }
}
