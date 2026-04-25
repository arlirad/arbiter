using System.Collections.Concurrent;
using System.Net;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Api.Middleware;

public class RateLimitMiddleware(HandleDelegate next, int maxRequests = 100, int windowSeconds = 60) : IMiddleware
{
    private readonly ConcurrentDictionary<string, RateLimitEntry> _clients = new();
    private readonly int _maxRequests = maxRequests;
    private readonly HandleDelegate _next = next;
    private readonly TimeSpan _window = TimeSpan.FromSeconds(windowSeconds);
    private string? _forwardedIpHeader;
    private HashSet<IPAddress> _ignoredAddresses = [];

    public Task Configure(Site site, IConfiguration config)
    {
        _forwardedIpHeader = config["ForwardedIpHeader"];

        var ignored = config.GetSection("IgnoredAddresses").Get<string[]>();
        if (ignored is not null)
        {
            _ignoredAddresses = [
                .. ignored.Select(a => IPAddress.TryParse(a, out var ip) ? ip : null)
                    .Where(ip => ip is not null)!
            ];
        }

        return Task.CompletedTask;
    }

    public async Task Handle(Context context)
    {
        if (context.Request.RemoteAddress is not null && _ignoredAddresses.Contains(context.Request.RemoteAddress))
        {
            await _next(context);
            return;
        }

        var key = ResolveClientIp(context);
        var now = DateTime.UtcNow;

        if (!_clients.TryGetValue(key, out var entry) || now - entry.WindowStart > _window)
        {
            entry = new RateLimitEntry {
                WindowStart = now,
                Count = 0
            };
            _clients[key] = entry;
        }
        else if (entry.Count >= _maxRequests)
        {
            await context.Response.Set(Status.TooManyRequests, Stream.Null);
            return;
        }

        entry.Count++;
        await _next(context);
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