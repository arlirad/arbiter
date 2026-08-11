namespace Arbiter.Infrastructure.Acme.Config;

public sealed record AcmeConfig
{
    public Uri AcmeDirectoryUrl { get; init; } = null!;
    public string AccountName { get; init; } = null!;
    public bool TosAccepted
    {
        get; init;
    }
}
