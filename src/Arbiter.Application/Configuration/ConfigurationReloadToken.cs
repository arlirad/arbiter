using System.Threading;
using Microsoft.Extensions.Primitives;

namespace Arbiter.Application.Configuration;

public class ConfigurationReloadToken : IChangeToken
{
    private readonly Lock _lock = new();
    private CancellationTokenSource _cts = new();

    public bool ActiveChangeCallbacks => true;

    public bool HasChanged => _cts.IsCancellationRequested;

    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
    {
        lock (_lock)
        {
            return _cts.Token.Register(callback, state);
        }
    }

    public CancellationToken WaitForChange(CancellationToken cancellationToken)
    {
        return _cts.Token.WaitHandle.WaitOne()
            ? CancellationToken.None
            : cancellationToken;
    }

    public void OnReload()
    {
        CancellationTokenSource oldCts;

        lock (_lock)
        {
            oldCts = _cts;
            _cts = new CancellationTokenSource();
        }

        try
        {
            oldCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        oldCts.Dispose();
    }
}