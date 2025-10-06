using Microsoft.Extensions.Configuration;

namespace Arbiter.Models.Config.Sites;

public class SiteComponentConfigModel
{
    public string? Name { get; set; }
    public IConfigurationSection? Config { get; set; }
}