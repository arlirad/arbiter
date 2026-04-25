using Arbiter.Core.Aggregates;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Core.Interfaces;

public interface IWorker
{
    Task Configure(Site site, IConfiguration config);
    Task Start();
    Task Stop();
}