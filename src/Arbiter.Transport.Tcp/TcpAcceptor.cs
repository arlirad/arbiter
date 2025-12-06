using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Arbiter.Application.Interfaces;

namespace Arbiter.Transport.Tcp;

public class TcpAcceptor(ICertificateManager certificateManager) : IAcceptor
{
    private const int Backlog = 128;

    private readonly Dictionary<IPEndPoint, TcpAcceptorSocket> _sockets = [];

    private readonly Channel<TcpTransaction> _transactions =
        Channel.CreateBounded<TcpTransaction>(new BoundedChannelOptions(4096));

    public async Task<ITransaction> Accept(CancellationToken ct)
    {
        while (true)
        {
            return await _transactions.Reader.ReadAsync(ct);
        }
    }

    public async Task Bind(IEnumerable<IPAddress> addresses, IEnumerable<int> ports)
    {
        var endPoints = new List<IPEndPoint>();

        foreach (var address in addresses)
        {
            foreach (var port in ports)
            {
                endPoints.Add(new IPEndPoint(address, port));
            }
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
            // ignored
        }
    }

    private async Task ConnectionLoop(Socket socket, CancellationToken ct)
    {
        try
        {
            Stream stream = new NetworkStream(socket);

            var secure = await CheckForSsl(socket);
            var port = (socket.LocalEndPoint as IPEndPoint)?.Port ?? 0;

            if (secure)
                stream = await WrapInSsl(stream);

            while (true)
            {
                var transaction = new TcpTransaction(stream, secure, port);

                await _transactions.Writer.WriteAsync(transaction, ct);
                await transaction.ResponseSet.WaitAsync(ct);

                if (transaction is { Finished: false, Faulted: false })
                    continue;

                socket.Dispose();
                break;
            }
        }
        catch (Exception _)
        {
            socket.Dispose();
        }
    }

    private static async Task<bool> CheckForSsl(Socket socket)
    {
        var buffer = new byte[1];
        var length = await socket.ReceiveAsync(buffer, SocketFlags.Peek);

        if (length == 0)
            return false;

        return buffer[0] == 22;
    }

    private async Task<Stream> WrapInSsl(Stream stream)
    {
        var ssl = new SslStream(stream, false);

        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificateSelectionCallback = CertificateSelectionCallback,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            ApplicationProtocols = [SslApplicationProtocol.Http11],
        });

        return ssl;
    }

    private X509Certificate2 CertificateSelectionCallback(object sender, string? hostName)
    {
        if (hostName is null)
            return certificateManager.GetFallback();

        return certificateManager.Get(hostName) ?? certificateManager.GetFallback();
    }

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
        var cancellationTasks = new List<Task>();
        var pruned = new Dictionary<IPEndPoint, TcpAcceptorSocket>();

        foreach (var socket in _sockets
            .Where(s => !endPoints.Any(e => e.Equals(s.Key))))
        {
            cancellationTasks.Add(socket.Value.Stop());
            pruned[socket.Key] = socket.Value;
        }

        foreach (var prunedSocket in pruned)
        {
            _sockets.Remove(prunedSocket.Key);
        }

        if (cancellationTasks.Count > 0)
            await Task.WhenAll(cancellationTasks);

        foreach (var prunedSocket in pruned)
        {
            prunedSocket.Value.Close();
        }
    }
}