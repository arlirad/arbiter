using Microsoft.Extensions.Configuration;

namespace Arbiter.Models.Config.Sites;

internal class SiteComponentConfigModel
{
    public string? Name { get; set; }
    public IConfigurationSection? Config { get; set; }
}