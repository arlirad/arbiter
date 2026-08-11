namespace Arbiter.Infrastructure.Config;

public sealed record StaticConfig
{
    public string Root { get; init; } = null!;
    public List<string>? DefaultFiles
    {
        get; init;
    }
    public Dictionary<string, string>? Mime
    {
        get; init;
    }
    public bool Fallthrough
    {
        get; init;
    }
}
