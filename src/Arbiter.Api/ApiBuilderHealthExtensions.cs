using Arbiter.Api.HealthChecks;

namespace Arbiter.Api;

public static class ApiBuilderHealthExtensions
{
    public static ApiBuilder UseHealthChecks(this ApiBuilder builder) => builder.UseMiddleware<HealthCheckMiddleware>();
}