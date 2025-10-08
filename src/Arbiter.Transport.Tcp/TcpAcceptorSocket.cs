using System.Net.Sockets;

namespace Arbiter.Transport.Tcp;

internal class TcpAcceptorSocket(Socket socket)
{
    private CancellationTokenSource _cts = new();
    public CancellationToken CancellationToken { get => _cts.Token; }

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