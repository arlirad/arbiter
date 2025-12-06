using System.Net.Quic;
using System.Runtime.Versioning;

namespace Arbiter.Transport.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
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