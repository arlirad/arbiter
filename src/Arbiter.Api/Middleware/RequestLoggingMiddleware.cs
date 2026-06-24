using System.Diagnostics;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Serilog;

namespace Arbiter.Api.Middleware;

public class RequestLoggingMiddleware(HandleDelegate next) : IMiddleware
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "api");

    public async Task Handle(Context context)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path;

        await next(context);

        stopwatch.Stop();

        Log.Information(
            "{Method} {Path} {StatusCode} {Duration}ms",
            method,
            path,
            (int)(context.Response.Status ?? Status.Ok),
            stopwatch.ElapsedMilliseconds
        );
    }
}
