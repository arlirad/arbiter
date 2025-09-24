using System.Net;
using System.Net.Sockets;

namespace Arbiter.Services;

public class Acceptor
{
    public const int Backlog = 128;

    private readonly List<IPAddress> _addresses = [];
    private readonly List<int> _ports = [];
    private readonly List<Socket> _sockets = [];
    private readonly Dictionary<Socket, Task<Socket>> _acceptTasks = [];

    public void Start()
    {
        foreach (ushort port in _ports)
        {
            var ipv4Addresses = _addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToList();
            var ipv6Addresses = _addresses.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6).ToList();

            Socket? socket4 = ipv4Addresses.Count > 0
                ? new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                : null;

            Socket? socket6 = ipv6Addresses.Count > 0
                ? new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
                : null;

            foreach (var address in ipv4Addresses)
                socket4!.Bind(new IPEndPoint(address, port));

            foreach (var address in ipv6Addresses)
                socket6!.Bind(new IPEndPoint(address, port));

            if (socket4 is not null)
                _sockets.Add(socket4);

            if (socket6 is not null)
                _sockets.Add(socket6);
        }

        foreach (var socket in _sockets)
        {
            socket.Listen(Backlog);
            _acceptTasks[socket] = socket.AcceptAsync();
        }
    }

    public async Task<Socket> Accept()
    {
        var completedTask = await Task.WhenAny(_acceptTasks.Select(kvp => kvp.Value));
        var acceptKvp = _acceptTasks
            .Where(kvp => kvp.Value == completedTask)
            .First();

        _acceptTasks[acceptKvp.Key] = acceptKvp.Key.AcceptAsync();

        return await completedTask;
    }

    public void Bind(IPAddress addr)
    {
        _addresses.Add(addr);
    }

    public void Bind(int port)
    {
        if (!_ports.Contains(port))
            _ports.Add(port);
    }
}
