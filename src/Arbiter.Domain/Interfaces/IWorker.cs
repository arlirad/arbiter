using Arbiter.Domain.Aggregates;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Domain.Interfaces;

public interface IWorker
{
    public Task Configure(Site site, IConfiguration config);
    public Task Start();
    public Task Stop();
}