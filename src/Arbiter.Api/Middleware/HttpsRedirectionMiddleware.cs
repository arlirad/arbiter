using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;

namespace Arbiter.Api.Middleware;

public class HttpsRedirectionMiddleware(HandleDelegate next, int httpsPort = 443) : IMiddleware
{
    public async Task Handle(Context context)
    {
        if (!context.Request.IsSecure)
        {
            var host = context.Request.Header("Host") ?? "localhost";
            var path = context.Request.Path;
            var redirectUrl = $"https://{host}:{httpsPort}{path}";

            context.Response.SetHeader("Location", redirectUrl);
            await context.Response.Set(Status.MovedPermanently, Stream.Null);

            return;
        }

        await next(context);
    }
}
