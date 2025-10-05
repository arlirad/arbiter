using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Arbiter.Models.Config;
using Arbiter.Services.Configurators;
using Microsoft.Extensions.Options;
using Serilog;

namespace Arbiter.Services;

internal class AcceptorSocket(Socket socket)
{
    private CancellationTokenSource _cts = new();

    public async Task<Socket> Accept()
    {
        return await socket.AcceptAsync(_cts.Token);
    }

    public async Task Stop()
    {
        var oldCts = _cts;
        _cts = new CancellationTokenSource();
        await oldCts.CancelAsync();
    }

    public void Close()
    {
        socket.Close();
        socket.Dispose();
    }
}

internal class TcpAcceptor : IAcceptor
{
    private const int Backlog = 128;
    private readonly Dictionary<AcceptorSocket, Task<Socket>> _acceptTasks = [];
    private readonly SemaphoreSlim _interrupter = new(1, 1);

    private readonly Dictionary<IPEndPoint, AcceptorSocket> _sockets = [];

    public async Task<Socket> Accept()
    {
        while (true)
        {
            try
            {
                var completedTask = await Task.WhenAny(_acceptTasks.Select(kvp => kvp.Value));
                var acceptKvp = _acceptTasks
                    .FirstOrDefault(kvp => kvp.Value == completedTask);

                if (acceptKvp.Key is null)
                    continue;

                _acceptTasks[acceptKvp.Key] = acceptKvp.Key.Accept();

                return await completedTask;
            }
            catch (OperationCanceledException)
            {
                continue;
            }
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
        await RestartAccepts();
    }

    private async Task RestartAccepts()
    {
        var tasks = _sockets.Select(socket => socket.Value.Stop()).ToList();

        await Task.WhenAll(tasks);
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

            var acceptorSocket = new AcceptorSocket(socket);

            _sockets[endPoint] = acceptorSocket;
            _acceptTasks[acceptorSocket] = acceptorSocket.Accept();
        }

        return Task.CompletedTask;
    }

    private async Task PruneSockets(IEnumerable<IPEndPoint> endPoints)
    {
        var cancellationTasks = new List<Task>();
        var pruned = new Dictionary<IPEndPoint, AcceptorSocket>();

        foreach (var socket in _sockets
            .Where(s => !endPoints.Any(e => e.Equals(s.Key))))
        {
            cancellationTasks.Add(socket.Value.Stop());
            pruned[socket.Key] = socket.Value;
        }

        foreach (var prunedSocket in pruned)
        {
            _sockets.Remove(prunedSocket.Key);
            _acceptTasks.Remove(prunedSocket.Value);
        }

        if (cancellationTasks.Count > 0)
            await Task.WhenAll(cancellationTasks);

        foreach (var prunedSocket in pruned)
        {
            prunedSocket.Value.Close();
        }
    }
}