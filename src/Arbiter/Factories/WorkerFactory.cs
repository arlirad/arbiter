using Arbiter.Middleware;
using Arbiter.Services;
using Arbiter.Workers;

namespace Arbiter.Factories;

internal class WorkerFactory(ComponentTypeRegistry<IWorker> workerTypeRegistry)
{
    public IWorker Create(string name)
    {
        var type = workerTypeRegistry.Get(name);
        if (type is null)
            throw new Exception($"Worker '{name}' not found");

        return (Activator.CreateInstance(type) as IWorker)!;
    }
}