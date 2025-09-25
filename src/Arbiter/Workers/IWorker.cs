using Arbiter.Models;

namespace Arbiter.Workers;

internal interface IWorker
{
    public string Name { get; }

    public Task Configure(Site site, object config);
    public Task Start();
    public Task Stop();
}