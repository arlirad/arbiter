using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Api.HealthChecks;

public interface IHealthCheck
{
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct);
}

public class HealthCheckResult
{
    public string Status
    {
        get;
        set;
    } = "Healthy";
    public Dictionary<string, HealthCheckResult> Checks
    {
        get;
        set;
    } = [];
    public TimeSpan Duration
    {
        get;
        set;
    }
}

public class HealthCheckMiddleware(HandleDelegate next, string healthPath = "/health") : IMiddleware
{
    private readonly string _healthPath = healthPath;
    private readonly HandleDelegate _next = next;

    public Task Configure(string path, ComponentDataContainer data, IConfiguration config) => Task.CompletedTask;

    public async Task Handle(Context context)
    {
        if (context.Request.Path == _healthPath && context.Request.Method == Method.Get)
        {
            await context.Response.Set(Status.Ok, new MemoryStream());

            return;
        }

        await _next(context);
    }
}
