namespace Arbiter.Infrastructure.Configuration;

internal class DefaultConfig
{
    public Dictionary<string, ComponentDefaultConfig>? Middleware { get; set; }
    public Dictionary<string, ComponentDefaultConfig>? Workers { get; set; }
}