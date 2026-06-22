namespace Arbiter.Infrastructure.Configuration;

internal class StaticMiddlewareConfig
{
    public string? Root
    {
        get;
        set;
    }
    public List<string>? DefaultFiles
    {
        get;
        set;
    }
    public Dictionary<string, string>? Mime
    {
        get;
        set;
    }
    public bool Fallthrough
    {
        get;
        set;
    }
}
