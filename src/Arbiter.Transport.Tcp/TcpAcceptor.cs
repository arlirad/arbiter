using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;
using Arbiter.Transport.Tcp.Models;
using Serilog;

namespace Arbiter.Transport.Tcp;

public class TcpAcceptor : IAcceptor, IAsyncConfigurable<TcpListenConfig>, IDisposable
{
    private const int Backlog = 128;

    private readonly ICertificateManager _certificateManager;
    private readonly ConfigurationProvider _configProvider;
    private readonly ConcurrentDictionary<IPEndPoint, TcpAcceptorSocket> _sockets = new();
    private readonly Channel<ITransport> _transports =
        Channel.CreateBounded<ITransport>(new BoundedChannelOptions(4096));
    private readonly CompositeDisposable _subscriptions = [];
    private readonly SemaphoreSlim _reconfigureLock = new(1, 1);

    public TcpAcceptor(ICertificateManager certificateManager, ConfigurationProvider configProvider)
    {
        _certificateManager = certificateManager;
        _configProvider = configProvider;

        var tcpConfig = Observable.CombineLatest(
            configProvider.Observe<List<string>>("ListenOn"),
            configProvider.Observe<Dictionary<string, SiteConfig>>("Sites"),
            (listenOn, sites) => {
                var ports = sites.SelectMany(s => s.Value.Bindings ?? [])
                    .Where(b => b.Scheme != "unix").Select(b => b.Port).ToList();
                var addresses = listenOn.Select(IPAddress.Parse).ToList();
                return new TcpListenConfig(addresses, ports);
            }
        );

        _subscriptions.Add(tcpConfig.Subscribe(async config => {
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
                Log.Error(ex, "Failed to reconfigure TCP acceptor");
            }
        }));
    }

    public async Task<ITransport> Accept(CancellationToken ct)
    {
        while (true)
            return await _transports.Reader.ReadAsync(ct);
    }

    public async ValueTask ReconfigureAsync(TcpListenConfig config) => await Bind(config.Addresses, config.Ports);

    public async Task Bind(IEnumerable<IPAddress> addresses, IEnumerable<int> ports)
    {
        var endPoints = new List<IPEndPoint>();

        foreach (var address in addresses)
        {
            foreach (var port in ports)
                endPoints.Add(new IPEndPoint(address, port));
        }

        await CreateSocket(endPoints);
        await PruneSockets(endPoints);
    }

    private async Task AcceptLoop(Socket socket, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var connection = await socket.AcceptAsync(ct);
                _ = ConnectionLoop(connection, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ConnectionLoop(Socket socket, CancellationToken ct)
    {
        try
        {
            Stream stream = new NetworkStream(socket);

            var secure = await CheckForSsl(socket);
            var port = (socket.LocalEndPoint as IPEndPoint)?.Port ?? 0;
            var remoteAddress = (socket.RemoteEndPoint as IPEndPoint)?.Address;

            if (secure)
                stream = await WrapInSsl(stream);

            var transport = new TcpTransport(stream, secure, port, remoteAddress);

            await _transports.Writer.WriteAsync(transport, ct);
        }
        catch (OperationCanceledException)
        {
            socket.Dispose();
        }
        catch (Exception)
        {
            socket.Dispose();
        }
    }

    private static async Task<bool> CheckForSsl(Socket socket)
    {
        var buffer = new byte[1];
        var length = await socket.ReceiveAsync(buffer, SocketFlags.Peek);

        return length != 0 && buffer[0] == 22;
    }

    private async Task<Stream> WrapInSsl(Stream stream)
    {
        var ssl = new SslStream(stream, false);

        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
            ServerCertificateSelectionCallback = CertificateSelectionCallback,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            ApplicationProtocols = [SslApplicationProtocol.Http11, SslApplicationProtocol.Http2],
        });

        return ssl;
    }

    private X509Certificate2 CertificateSelectionCallback(object sender, string? hostName)
        => hostName is null ? _certificateManager.GetFallback() : _certificateManager.Get(hostName) ?? _certificateManager.GetFallback();

    private Task CreateSocket(List<IPEndPoint> endPoints)
    {
        foreach (var endPoint in endPoints)
        {
            if (_sockets.ContainsKey(endPoint))
                continue;

            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(endPoint);
            socket.Listen(Backlog);

            var acceptorSocket = new TcpAcceptorSocket(socket);

            _sockets[endPoint] = acceptorSocket;
            _ = AcceptLoop(socket, acceptorSocket.CancellationToken);
        }

        return Task.CompletedTask;
    }

    private async Task PruneSockets(IEnumerable<IPEndPoint> endPoints)
    {
        var toRemove = _sockets.Keys
            .Where(e => !endPoints.Any(ep => ep.Equals(e)))
            .ToList();

        var cancellationTasks = new List<Task>();

        foreach (var endpoint in toRemove)
        {
            if (_sockets.TryRemove(endpoint, out var socket))
            {
                cancellationTasks.Add(socket.Stop());
                _ = Task.Run(socket.Close);
            }
        }

        if (cancellationTasks.Count > 0)
            await Task.WhenAll(cancellationTasks);
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _reconfigureLock.Dispose();
    }
}
