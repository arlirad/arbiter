namespace Arbiter.Infrastructure.Configuration;

internal class StaticMiddlewareConfig
{
    public List<string>? DefaultFiles { get; set; }
    public Dictionary<string, string>? Mime { get; set; }
}