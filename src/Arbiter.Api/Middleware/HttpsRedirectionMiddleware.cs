using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Api.Middleware;

public class HttpsRedirectionMiddleware(HandleDelegate next, int httpsPort = 443) : IMiddleware
{
    private readonly int _httpsPort = httpsPort;
    private readonly HandleDelegate _next = next;

    public Task Configure(Site site, IConfiguration config) => Task.CompletedTask;

    public async Task Handle(Context context)
    {
        if (!context.Request.IsSecure)
        {
            var host = context.Request.Headers["Host"]?.FirstOrDefault() ?? "localhost";
            var path = context.Request.Path;
            var redirectUrl = $"https://{host}:{_httpsPort}{path}";

            context.Response.Headers["Location"] = [redirectUrl];
            await context.Response.Set(Status.MovedPermanently, Stream.Null);
            return;
        }

        await _next(context);
    }
}