using Arbiter.Core.Aggregates;
using Arbiter.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Interfaces;

public interface IConfigurableMiddleware
{
    Task Configure(ComponentDataContainer data, IConfiguration config);
}

public interface IConfigurableMiddleware<TConfig> : IMiddleware, IConfigurableMiddleware
    where TConfig : new()
{
    Task Configure(ComponentDataContainer data, TConfig config);

    Task IConfigurableMiddleware.Configure(ComponentDataContainer data, IConfiguration config)
        => Configure(data, config.Get<TConfig>() ?? new TConfig());
}
