using Arbiter.Middleware;
using Arbiter.Services;
using Arbiter.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Factories;

internal class WorkerFactory
{
    public IWorker Create(string name, IServiceScope scope)
    {
        return scope.ServiceProvider.GetKeyedService<IWorker>(name) 
            ?? throw new Exception($"Worker '{name}' not found");
    }
}