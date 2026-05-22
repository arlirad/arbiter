using System.Net.Sockets;

namespace Arbiter.Transport.Unix;

internal class UnixSocketAcceptorSocket(Socket socket, string path)
{
    private CancellationTokenSource _cts = new();
    public CancellationToken CancellationToken => _cts.Token;
    public string Path => path;

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

        if (File.Exists(Path))
        {
            try
            {
                File.Delete(Path);
            }
            catch
            {
                // ignored
            }
        }
    }
}
