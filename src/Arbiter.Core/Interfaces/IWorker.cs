using Arbiter.Core.Aggregates;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Core.Interfaces;

public interface IWorker
{
    Task Configure(List<Uri> bindings, ComponentDataContainer data, IConfiguration config);
    Task Start();
    Task Stop();
}
