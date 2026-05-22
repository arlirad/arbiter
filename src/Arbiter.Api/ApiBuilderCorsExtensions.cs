using Arbiter.Infrastructure.Cors;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Api;

public static class ApiBuilderCorsExtensions
{
    public static ApiBuilder UseCors(this ApiBuilder builder, Action<CorsOptions>? configure = null)
    {
        var options = new CorsOptions();
        configure?.Invoke(options);

        IConfiguration? config = null;

        if (options.HasValues)
        {
            config = new ConfigurationBuilder()
                .AddInMemoryCollection(options.ToConfigDictionary())
                .Build();
        }

        return builder.UseMiddleware<CorsMiddleware>(config);
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

    internal IDictionary<string, string?> ToConfigDictionary()
    {
        var dict = new Dictionary<string, string?>();

        for (var i = 0; i < AllowOrigins.Count; i++)
            dict[$"AllowOrigin:{i}"] = AllowOrigins[i];

        for (var i = 0; i < AllowMethods.Count; i++)
            dict[$"AllowMethods:{i}"] = AllowMethods[i];

        for (var i = 0; i < AllowHeaders.Count; i++)
            dict[$"AllowHeaders:{i}"] = AllowHeaders[i];

        if (AllowCredentials.HasValue)
            dict["AllowCredentials"] = AllowCredentials.Value.ToString().ToLowerInvariant();

        return dict;
    }
}
