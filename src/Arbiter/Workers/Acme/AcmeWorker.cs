using Arbiter.Models;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Workers.Acme;

internal class AcmeWorker : IWorker
{
    public string Name { get; }
    
    public Task Configure(Site site, IConfiguration config)
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