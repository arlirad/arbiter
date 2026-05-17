using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Api.Middleware;

public class HttpsRedirectionMiddleware(HandleDelegate next, int httpsPort = 443) : IMiddleware
{
    public Task Configure(string path, ComponentDataContainer data, IConfiguration config) => Task.CompletedTask;

    public async Task Handle(Context context)
    {
        if (!context.Request.IsSecure)
        {
            var host = context.Request.Headers["Host"]?.FirstOrDefault() ?? "localhost";
            var path = context.Request.Path;
            var redirectUrl = $"https://{host}:{httpsPort}{path}";

            context.Response.Headers["Location"] = [redirectUrl];
            await context.Response.Set(Status.MovedPermanently, Stream.Null);
            return;
        }

        await next(context);
    }
}