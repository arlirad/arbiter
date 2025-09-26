using Arbiter.Models.Config.Sites;

namespace Arbiter.Models.Config;

internal class ConfigModel
{
    public Dictionary<string, SiteConfigModel>? Sites { get; set; }
    public List<string>? ListenOn { get; set; }
    public Dictionary<string, string>? Mime { get; set; }
}