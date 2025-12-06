using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Configuration;

public class SiteComponentConfig
{
    public string? Name { get; set; }
    public IConfigurationSection? Config { get; set; }
}