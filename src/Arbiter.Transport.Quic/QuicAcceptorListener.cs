using System.Net.Quic;
using System.Runtime.Versioning;

namespace Arbiter.Transport.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
internal class QuicAcceptorListener(QuicListener listener)
{
    private CancellationTokenSource _cts = new();
    public CancellationToken CancellationToken => _cts.Token;

    public async Task Stop()
    {
        var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        await oldCts.CancelAsync();
        oldCts.Dispose();
    }

    public async Task Close() => await listener.DisposeAsync();
}