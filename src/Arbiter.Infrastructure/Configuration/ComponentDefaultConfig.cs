using Microsoft.Extensions.Configuration;

namespace Arbiter.Infrastructure.Configuration;

internal class ComponentDefaultConfig
{
    public IConfigurationSection? Config { get; set; }
}