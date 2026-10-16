namespace Arbiter.Api.Middleware;

public sealed record RateLimitConfig
{
    public int MaxRequests { get; init; } = 100;
    public int WindowSeconds { get; init; } = 60;
    public string? ForwardedIpHeader
    {
        get; init;
    }
    public List<string>? IgnoredAddresses
    {
        get; init;
    }
}
