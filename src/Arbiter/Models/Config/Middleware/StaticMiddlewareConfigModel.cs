namespace Arbiter.Models.Config.Middleware;

internal class StaticMiddlewareConfigModel
{
    public List<string>? DefaultFiles { get; set; }
    public Dictionary<string, string>? Mime { get; set; }
}