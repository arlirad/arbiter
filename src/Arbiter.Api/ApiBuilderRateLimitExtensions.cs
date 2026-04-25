using Arbiter.Api.Middleware;
using Microsoft.Extensions.Configuration;

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
        var configurationBuilder = new ConfigurationBuilder();
        var dict = new Dictionary<string, string?> {
            ["MaxRequests"] = maxRequests.ToString(),
            ["WindowSeconds"] = windowSeconds.ToString(),
            ["ForwardedIpHeader"] = forwardedIpHeader,
        };

        if (ignoredAddresses is not null)
        {
            for (var i = 0; i < ignoredAddresses.Length; i++)
                dict[$"IgnoredAddresses:{i}"] = ignoredAddresses[i];
        }

        configurationBuilder.AddInMemoryCollection(dict);
        var config = configurationBuilder.Build();

        return builder.UseMiddleware<RateLimitMiddleware>(config);
    }
}