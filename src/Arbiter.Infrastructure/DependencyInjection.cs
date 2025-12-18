using Arbiter.Application.Interfaces;
using Arbiter.Domain.Interfaces;
using Arbiter.Infrastructure.Configuration;
using Arbiter.Infrastructure.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IConfigManager, ConfigManager>();
        services.AddKeyedScoped<IMiddleware, StaticMiddleware>("static");
    }

    public static void AddConfiguration(this IServiceCollection services, string[] args)
    {
        var basePath = args.Any(s => s == "--local-config")
            ? Path.Combine(Directory.GetCurrentDirectory(), "./cfg/arbiter")
            : "/etc/arbiter/arbiter";

        var yamlPath = $"{basePath}.yaml";
        var jsonPath = $"{basePath}.json";

        var builder = new ConfigurationBuilder();

        if (File.Exists(yamlPath))
            builder.AddYamlFile(yamlPath, optional: false, reloadOnChange: true);
        else if (File.Exists(jsonPath))
            builder.AddJsonFile(jsonPath, optional: false, reloadOnChange: true);
        else
            builder.AddYamlFile(yamlPath, optional: false, reloadOnChange: true);

        services.AddSingleton<IConfiguration>(builder.Build());
    }
}