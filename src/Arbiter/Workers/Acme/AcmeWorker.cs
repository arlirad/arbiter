using Arbiter.Models;

namespace Arbiter.Workers.Acme;

internal class AcmeWorker : IWorker
{
    public string Name { get; }
    
    public Task Configure(Site site, object config)
    {
        return Task.CompletedTask;
    }

    public Task Start()
    {
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        return Task.CompletedTask;
    }
}