using Arbiter.Application.Interfaces;
using Arbiter.Domain.Aggregates;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Application.Middleware;

public static class GlobalMiddlewareInjection
{
    private static readonly List<Type> Types = [];
    private static readonly Dictionary<Type, object> Instances = [];
    private static HandleDelegate? _handleDelegate;

    public static void AddGlobalMiddleware<T>(this IServiceCollection services) where T : class, IGlobalMiddleware
    {
        Types.Add(typeof(T));
        services.AddSingleton<T>(sp =>
        {
            BuildChain(sp);
            return (Instances[typeof(T)] as T)!;
        });
    }

    public static HandleDelegate GetHandleDelegate(IServiceProvider sp)
    {
        BuildChain(sp);
        return _handleDelegate!;
    }

    private static void BuildChain(IServiceProvider sp)
    {
        if (_handleDelegate is not null)
            return;

        HandleDelegate previousDelegate = LastHandleDelegate;

        foreach (var middleware in Types.AsEnumerable().Reverse())
        {
            var instance = ActivatorUtilities.CreateInstance(sp, middleware, previousDelegate) as IGlobalMiddleware;

            Instances[instance!.GetType()] = instance;
            previousDelegate = instance!.Handle;
        }

        _handleDelegate = previousDelegate;
    }

    private static Task LastHandleDelegate(ITransaction _, Site? site, Context context)
    {
        return site?.HandleDelegate(context) ?? Task.CompletedTask;
    }
}