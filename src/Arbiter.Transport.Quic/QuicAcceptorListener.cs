using System.Net.Quic;

namespace Arbiter.Transport.Quic;

internal class QuicAcceptorListener(QuicListener listener)
{
    private CancellationTokenSource _cts = new();
    public CancellationToken CancellationToken { get => _cts.Token; }

    public async Task Stop()
    {
        var oldCts = _cts;
        _cts = new CancellationTokenSource();
        await oldCts.CancelAsync();
    }

    public async Task Close()
    {
        await listener.DisposeAsync();
    }
}