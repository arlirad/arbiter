using Microsoft.Extensions.Configuration;

namespace Arbiter.Models.Config;

internal class ComponentDefaultConfigModel
{
    public IConfigurationSection? Config { get; set; }
}