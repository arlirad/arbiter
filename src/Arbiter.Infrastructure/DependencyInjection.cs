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
        var configPath = args.Any(s => s == "--local-config")
            ? Path.Combine(Directory.GetCurrentDirectory(), "./cfg/arbiter.json")
            : "/etc/arbiter/arbiter.json";

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
    }
}