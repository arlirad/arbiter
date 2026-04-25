using Arbiter.Infrastructure.Middleware;

namespace Arbiter.Api;

public static class ApiBuilderStaticExtensions
{
    public static ApiBuilder UseStatic(this ApiBuilder builder) => builder.UseMiddleware<StaticMiddleware>();
}