namespace Arbiter.Models.Config;

internal class ConfigModel
{
    public Dictionary<string, Site>? Sites { get; set; }
    public List<string>? ListenOn { get; set; }
    public Dictionary<string, string>? Mime { get; set; }
}