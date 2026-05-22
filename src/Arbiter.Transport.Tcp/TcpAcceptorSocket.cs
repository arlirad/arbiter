using System.Net.Sockets;

namespace Arbiter.Transport.Tcp;

internal class TcpAcceptorSocket(Socket socket)
{
    private CancellationTokenSource _cts = new();
    public CancellationToken CancellationToken => _cts.Token;

    public async Task Stop()
    {
        var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        await oldCts.CancelAsync();
        oldCts.Dispose();
    }

    public void Close()
    {
        socket.Close();
        socket.Dispose();
    }
}
