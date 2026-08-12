using Arbiter.Api.Middleware;

namespace Arbiter.Api;

public static class ApiBuilderRateLimitExtensions
{
    public static ApiBuilder UseRateLimiting(
        this ApiBuilder builder,
        int maxRequests,
        int windowSeconds,
        string? forwardedIpHeader = null,
        string[]? ignoredAddresses = null)
    {
        var config = new RateLimitConfig {
            MaxRequests = maxRequests,
            WindowSeconds = windowSeconds,
            ForwardedIpHeader = forwardedIpHeader,
            IgnoredAddresses = ignoredAddresses?.ToList(),
        };

        return builder.UseMiddleware<RateLimitMiddleware, RateLimitConfig>(config);
    }
}
