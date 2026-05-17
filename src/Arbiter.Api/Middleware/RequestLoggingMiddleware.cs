using System.Diagnostics;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Api.Middleware;

public class RequestLoggingMiddleware(HandleDelegate next) : IMiddleware
{
    public Task Configure(string path, ComponentDataContainer data, IConfiguration config) => Task.CompletedTask;

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