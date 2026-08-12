using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;

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
    public async Task Handle(Context context)
    {
        if (context.Request.Path == healthPath && context.Request.Method == Method.Get)
        {
            await context.Response.Set(Status.Ok, new MemoryStream());

            return;
        }

        await next(context);
    }
}
