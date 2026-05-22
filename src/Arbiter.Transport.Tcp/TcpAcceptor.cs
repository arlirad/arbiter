using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;
using Arbiter.Core.Enums;
using Arbiter.Transport.Configuration;
using Serilog;

namespace Arbiter.Transport.Tcp;

public class TcpAcceptor(ICertificateManager certificateManager) : IAcceptor, IAsyncConfigurable<List<IPAddress>, IpTransportConfig, HashSet<Protocol>>, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "tcp");
    private readonly ConcurrentDictionary<IPEndPoint, TcpAcceptorSocket> _sockets = new();
    private Channel<ITransport>? _transports;

    public async Task<ITransport> Accept(CancellationToken ct) => await _transports!.Reader.ReadAsync(ct);

    public async ValueTask ReconfigureAsync(List<IPAddress> addresses, IpTransportConfig config, HashSet<Protocol> protocols)
    {
        var hasHttp11 = protocols.Contains(Protocol.Http11);
        var hasHttp2 = protocols.Contains(Protocol.Http2);

        if (!hasHttp11 && !hasHttp2)
            return;

        if (config.Ports is null || config.Ports.Count == 0)
            Log.Warning("No ports configured");

        _transports ??= Channel.CreateBounded<ITransport>(new BoundedChannelOptions(config.QueueSize));
        await Bind(addresses, config.Ports, config.Backlog);
    }

    public void Dispose()
    {
        foreach (var socket in _sockets.Values)
        {
            socket.Stop();
            socket.Close();
        }

        _sockets.Clear();
    }

    public async Task Bind(IEnumerable<IPAddress> addresses, IEnumerable<int> ports, int backlog)
    {
        _transports ??= Channel.CreateBounded<ITransport>(new BoundedChannelOptions(4096));
        var endPoints = new List<IPEndPoint>();

        foreach (var address in addresses)
        {
            foreach (var port in ports)
                endPoints.Add(new IPEndPoint(address, port));
        }

        await CreateSocket(endPoints, backlog);
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
        var buffer = ArrayPool<byte>.Shared.Rent(1);

        try
        {
            var length = await socket.ReceiveAsync(buffer, SocketFlags.Peek);

            return length != 0 && buffer[0] == 22;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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
        => hostName is null ? certificateManager.GetFallback() : certificateManager.Get(hostName) ?? certificateManager.GetFallback();

    private Task CreateSocket(List<IPEndPoint> endPoints, int backlog)
    {
        var newEndpoints = endPoints.Where(e => !_sockets.ContainsKey(e)).ToList();

        if (newEndpoints.Count > 0)
            Log.Information("Binding {Count} endpoint(s): {Endpoints}", newEndpoints.Count, newEndpoints);

        foreach (var endPoint in newEndpoints)
        {
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(endPoint);
            socket.Listen(backlog);

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

        if (toRemove.Count > 0)
            Log.Information("Pruning {Count} endpoint(s): {Endpoints}", toRemove.Count, toRemove);

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
}
