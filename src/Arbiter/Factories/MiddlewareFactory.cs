using Arbiter.Middleware;
using Arbiter.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Factories;

internal class MiddlewareFactory(IServiceProvider serviceProvider)
{
    public IMiddleware Create(string name)
    {
        return serviceProvider.GetKeyedService<IMiddleware>(name)
            ?? throw new Exception($"Middleware '{name}' not found");
    }
}