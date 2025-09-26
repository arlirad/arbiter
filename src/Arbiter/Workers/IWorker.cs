using Arbiter.Models;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Workers;

internal interface IWorker
{
    public Task Configure(Site site, IConfiguration config);
    public Task Start();
    public Task Stop();
}