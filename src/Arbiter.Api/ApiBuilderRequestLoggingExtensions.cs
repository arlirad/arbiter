using Arbiter.Api.Middleware;

namespace Arbiter.Api;

public static class ApiBuilderRequestLoggingExtensions
{
    public static ApiBuilder UseRequestLogging(this ApiBuilder builder) => builder.UseMiddleware<RequestLoggingMiddleware>();
}
