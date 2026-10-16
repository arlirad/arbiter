using System.Collections.Concurrent;
using System.Net;
using Arbiter.Api.Middleware;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using HandleDelegate = Arbiter.Core.Interfaces.HandleDelegate;

namespace Arbiter.Api.Middleware;

public class RateLimitMiddleware(HandleDelegate next) : IConfigurableMiddleware<RateLimitConfig>
{
    private readonly ConcurrentDictionary<string, RateLimitEntry> _clients = new();
    private int _maxRequests = 100;
    private TimeSpan _window = TimeSpan.FromSeconds(60);
    private string? _forwardedIpHeader;
    private HashSet<IPAddress> _ignoredAddresses = [];

    public Task Configure(ComponentDataContainer data, RateLimitConfig config)
    {
        _maxRequests = config.MaxRequests;
        _window = TimeSpan.FromSeconds(config.WindowSeconds);
        _forwardedIpHeader = config.ForwardedIpHeader;

        if (config.IgnoredAddresses is not null)
        {
            _ignoredAddresses = [
                .. config.IgnoredAddresses
                    .Select(a => IPAddress.TryParse(a, out var ip) ? ip : null)
                    .OfType<IPAddress>(),
            ];
        }

        return Task.CompletedTask;
    }

    public async Task Handle(Context context)
    {
        if (context.Request.RemoteAddress is not null && _ignoredAddresses.Contains(context.Request.RemoteAddress))
        {
            await next(context);

            return;
        }

        var key = ResolveClientIp(context);
        var now = DateTime.UtcNow;

        if (!_clients.TryGetValue(key, out var entry) || now - entry.WindowStart > _window)
        {
            entry = new RateLimitEntry {
                WindowStart = now,
                Count = 0,
            };

            _clients[key] = entry;
        }
        else if (entry.Count >= _maxRequests)
        {
            await context.Response.Set(Status.TooManyRequests, Stream.Null);

            return;
        }

        entry.Count++;
        await next(context);
    }

    private string ResolveClientIp(Context context)
    {
        if (_forwardedIpHeader is not null)
        {
            var headerValues = context.Request.Headers[_forwardedIpHeader];
            var headerValue = headerValues?.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                if (_forwardedIpHeader.Equals("X-Forwarded-For", StringComparison.OrdinalIgnoreCase))
                    headerValue = headerValue.Split(',').First().Trim();

                return headerValue;
            }
        }

        return context.Request.RemoteAddress?.ToString() ?? "unknown";
    }

    private class RateLimitEntry
    {
        public DateTime WindowStart
        {
            get;
            set;
        }
        public int Count
        {
            get;
            set;
        }
    }
}
