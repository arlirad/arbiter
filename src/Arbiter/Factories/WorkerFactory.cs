using Arbiter.Middleware;
using Arbiter.Services;
using Arbiter.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Factories;

internal class WorkerFactory(IServiceProvider serviceProvider)
{
    public IWorker Create(string name)
    {
        return serviceProvider.GetKeyedService<IWorker>(name)
            ?? throw new Exception($"Worker '{name}' not found");
    }
}