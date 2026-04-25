using Microsoft.Extensions.Primitives;

namespace Arbiter.Application.Configuration;

public class ConfigurationReloadToken : IChangeToken
{
    private readonly CancellationTokenSource _cts = new();

    public bool ActiveChangeCallbacks => true;

    public bool HasChanged => _cts.IsCancellationRequested;

    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => _cts.Token.Register(callback, state);

    public CancellationToken WaitForChange(CancellationToken cancellationToken)
    {
        return _cts.Token.WaitHandle.WaitOne()
            ? CancellationToken.None
            : cancellationToken;
    }

    public void OnReload()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignored
        }
    }
}