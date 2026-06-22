using Arbiter.Application.Interfaces;
using Arbiter.Core.Interfaces;
using Arbiter.Infrastructure.Configuration;
using Arbiter.Infrastructure.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, string[] args)
    {
        services.AddSingleton<IConfigManager, ConfigManager>();
        services.AddKeyedScoped<IMiddleware, StaticMiddleware>("static");
        services.AddSingleton<TransactionIdProvider>();

        var sitesDirectory = args.Any(s => s == "--local-config")
            ? Path.Combine(Directory.GetCurrentDirectory(), "./cfg/sites")
            : "/etc/sites";

        services.AddSingleton<ISitesProvider>(sp =>
            new SitesProvider(
                sp.GetRequiredService<Arbiter.Configuration.ConfigurationProvider>(),
                sitesDirectory));
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
            builder.AddYamlFile(yamlPath, false, true);
        else if (File.Exists(jsonPath))
            builder.AddJsonFile(jsonPath, false, true);
        else
            builder.AddYamlFile(yamlPath, false, true);

        services.AddSingleton<IConfiguration>(builder.Build());
    }
}
