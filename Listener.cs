using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Arbiter;

public class Listener
{
    public delegate void OnConnectionHandler(object sender, Socket socket);
    public event OnConnectionHandler? OnConnection;

    private List<IPAddress> _addresses = new List<IPAddress>();
    private List<int> _ports = new List<int>();
    private List<Socket> _sockets = new List<Socket>();

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
            {
                socket4!.Bind(new IPEndPoint(address, port));
            }

            foreach (var address in ipv6Addresses)
            {
                socket6!.Bind(new IPEndPoint(address, port));
            }

            if (socket4 is not null)
            {
                InitialAccept(socket4);
            }

            if (socket6 is not null)
            {
                InitialAccept(socket6);
            }
        }
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

    private void InitialAccept(Socket socket)
    {
        var acceptEventArgs = new SocketAsyncEventArgs();
        acceptEventArgs.Completed += AcceptEventArgs_Completed;

        socket.Listen();

        if (!socket.AcceptAsync(acceptEventArgs))
            AcceptEventArgs_Completed(socket, acceptEventArgs);

        _sockets.Add(socket);
    }

    private void AcceptEventArgs_Completed(object? sender, SocketAsyncEventArgs e)
    {
        if (sender == null)
            throw new Exception("sender borke");

        var socket = e.AcceptSocket;
        if (socket == null)
            throw new Exception("socket borke");

        OnConnection?.Invoke(this, socket);

        e.AcceptSocket = null; // unix hhh
        if (!((Socket)sender).AcceptAsync(e))
            AcceptEventArgs_Completed(sender, e);
    }
}
