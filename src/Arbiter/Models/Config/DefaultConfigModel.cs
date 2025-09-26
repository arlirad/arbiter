using Microsoft.Extensions.Configuration;

namespace Arbiter.Models.Config;

internal class DefaultConfigModel
{
    public Dictionary<string, ComponentDefaultConfigModel> Middleware { get; set; }
    public Dictionary<string, ComponentDefaultConfigModel> Workers { get; set; }
}