using Arbiter.Core.Aggregates;
using Arbiter.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Interfaces;

public interface IConfigurableWorker
{
    Task Configure(List<Uri> bindings, ComponentDataContainer data, IConfiguration config);
}

public interface IConfigurableWorker<TConfig> : IWorker, IConfigurableWorker
    where TConfig : new()
{
    Task Configure(List<Uri> bindings, ComponentDataContainer data, TConfig config);

    Task IConfigurableWorker.Configure(List<Uri> bindings, ComponentDataContainer data, IConfiguration config)
        => Configure(bindings, data, config.Get<TConfig>() ?? new TConfig());
}
