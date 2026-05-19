using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Application.Middleware;

public sealed class GlobalMiddlewareChain
{
    private readonly List<Func<HandleDelegate, HandleDelegate>> _factories = [];

    public HandleDelegate Delegate { get; set; } = LastHandle;

    public void Add(Func<HandleDelegate, HandleDelegate> factory) => _factories.Add(factory);

    public void Build(IServiceProvider sp)
    {
        var next = (HandleDelegate)LastHandle;

        var types = sp.GetRequiredService<IEnumerable<GlobalMiddlewareDescriptor>>();
        foreach (var descriptor in types.AsEnumerable().Reverse())
        {
            var instance = (ActivatorUtilities.CreateInstance(sp, descriptor.Type, next) as IGlobalMiddleware)!;
            next = instance.Handle;
        }

        foreach (var factory in _factories.AsEnumerable().Reverse())
            next = factory(next);

        Delegate = next;
    }

    public void Rebuild(IServiceProvider sp, Action<GlobalMiddlewareChain> configure)
    {
        _factories.Clear();
        configure(this);
        Build(sp);
    }

    private static Task LastHandle(ITransaction _, Site? site, Context context) => site?.HandleDelegate(context) ?? Task.CompletedTask;
}
