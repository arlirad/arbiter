using Arbiter.Api.Middleware;

namespace Arbiter.Api;

public static class ApiBuilderHttpsExtensions
{
    public static ApiBuilder UseHttpsRedirection(this ApiBuilder builder, int httpsPort = 443)
        => builder.UseMiddleware<HttpsRedirectionMiddleware>();
}
