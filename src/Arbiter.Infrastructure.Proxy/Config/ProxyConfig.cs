namespace Arbiter.Infrastructure.Proxy.Config;

public sealed record ProxyConfig
{
    public Uri Target { get; init; } = null!;
}
