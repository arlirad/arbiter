using Arbiter.Infrastructure.Cors;
using Arbiter.Infrastructure.Cors.Config;

namespace Arbiter.Api;

public static class ApiBuilderCorsExtensions
{
    public static ApiBuilder UseCors(this ApiBuilder builder, Action<CorsOptions>? configure = null)
    {
        var options = new CorsOptions();
        configure?.Invoke(options);

        if (!options.HasValues)
            return builder.UseMiddleware<CorsMiddleware>();

        var config = new CorsConfig {
            AllowOrigin = options.AllowOrigins.Count > 0 ? options.AllowOrigins : null,
            AllowMethods = options.AllowMethods.Count > 0 ? options.AllowMethods : null,
            AllowHeaders = options.AllowHeaders.Count > 0 ? options.AllowHeaders : null,
            AllowCredentials = options.AllowCredentials,
        };

        return builder.UseMiddleware<CorsMiddleware, CorsConfig>(config);
    }
}

public class CorsOptions
{
    public List<string> AllowOrigins
    {
        get;
    } = [];
    public List<string> AllowMethods
    {
        get;
    } = [];
    public List<string> AllowHeaders
    {
        get;
    } = [];
    public bool? AllowCredentials
    {
        get;
        set;
    }

    internal bool HasValues
        => AllowOrigins.Count > 0 ||
            AllowMethods.Count > 0 ||
            AllowHeaders.Count > 0 ||
            AllowCredentials.HasValue;
}
