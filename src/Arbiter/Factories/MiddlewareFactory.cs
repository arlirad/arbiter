using Arbiter.Middleware;
using Arbiter.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Factories;

internal class MiddlewareFactory(IServiceProvider services, ComponentTypeRegistry<IMiddleware> middlewareTypeRegistry)
{
    public IMiddleware Create(string name)
    {
        var type = middlewareTypeRegistry.Get(name);
        if (type is null)
            throw new Exception($"Middleware '{name}' not found");

        return (ActivatorUtilities.CreateInstance(services, type) as IMiddleware)!;
    }
}