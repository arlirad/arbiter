using Arbiter.Middleware;
using Arbiter.Services;

namespace Arbiter.Factories;

internal class MiddlewareFactory(ComponentTypeRegistry<IMiddleware> middlewareTypeRegistry)
{
    public IMiddleware Create(string name)
    {
        var type = middlewareTypeRegistry.Get(name);
        if (type is null)
            throw new Exception($"Middleware '{name}' not found");

        return (Activator.CreateInstance(type) as IMiddleware)!;
    }
}