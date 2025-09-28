using Arbiter.Middleware;
using Arbiter.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Factories;

internal class MiddlewareFactory
{
    public IMiddleware Create(string name, IServiceScope scope)
    {
        return scope.ServiceProvider.GetKeyedService<IMiddleware>(name) 
            ?? throw new Exception($"Middleware '{name}' not found");
    }
}