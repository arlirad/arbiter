using Arbiter.Models.Config.Sites;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Models.Config;

internal class ServerConfigModel
{
    public Dictionary<string, SiteConfigModel>? Sites { get; set; }
    public List<string>? ListenOn { get; set; }
    public DefaultConfigModel? Default { get; set; }
}