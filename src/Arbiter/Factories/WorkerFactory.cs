using Arbiter.Middleware;
using Arbiter.Services;
using Arbiter.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Factories;

internal class WorkerFactory(IServiceProvider services, ComponentTypeRegistry<IWorker> workerTypeRegistry)
{
    public IWorker Create(string name)
    {
        var type = workerTypeRegistry.Get(name);
        if (type is null)
            throw new Exception($"Worker '{name}' not found");

        return (ActivatorUtilities.CreateInstance(services, type) as IWorker)!;
    }
}