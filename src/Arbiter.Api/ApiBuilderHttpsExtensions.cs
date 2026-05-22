using Arbiter.Api.Middleware;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Api;

public static class ApiBuilderHttpsExtensions
{
    public static ApiBuilder UseHttpsRedirection(this ApiBuilder builder, int httpsPort = 443)
    {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?> {
            ["HttpsPort"] = httpsPort.ToString(),
        });

        var config = configurationBuilder.Build();

        return builder.UseMiddleware<HttpsRedirectionMiddleware>(config);
    }
}
