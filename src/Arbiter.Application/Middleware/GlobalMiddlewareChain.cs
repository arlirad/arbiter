using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Application.Middleware;

public sealed class GlobalMiddlewareChain
{
    private volatile HandleDelegate _delegate = LastHandle;

    public HandleDelegate Delegate
    {
        get => _delegate;
        set => _delegate = value;
    }

    public void Build(IServiceProvider sp)
    {
        var next = (HandleDelegate)LastHandle;

        foreach (var factory in sp.GetRequiredService<IEnumerable<IGlobalMiddlewareFactory>>())
            next = factory.Create(next);

        _delegate = next;
    }

    private static Task LastHandle(ITransaction _, Site? site, Context context) => site?.HandleDelegate(context) ?? Task.CompletedTask;
}
