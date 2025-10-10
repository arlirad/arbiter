using Arbiter.Application.Interfaces;
using Arbiter.Domain.Interfaces;
using Arbiter.Infrastructure.Configuration;
using Arbiter.Infrastructure.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Infrastructure;

public static class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IConfigManager, ConfigManager>();
        services.AddKeyedScoped<IMiddleware, StaticMiddleware>("static");
    }
}