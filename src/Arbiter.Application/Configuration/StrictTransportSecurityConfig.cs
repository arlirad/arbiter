namespace Arbiter.Application.Configuration;

public class StrictTransportSecurityConfig
{
    public int MaxAge { get; init; } = 31536000;
    public bool IncludeSubDomains
    {
        get; init;
    }
    public bool Preload
    {
        get; init;
    }
}