using Arbiter.Application.Interfaces;
using Arbiter.Infrastructure.Headers.Factories;
using Arbiter.Infrastructure.Headers.Managers;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Infrastructure.Headers;

public static class DependencyInjection
{
    public static IServiceCollection AddHeadersInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IGlobalMiddlewareFactory, AltSvcGlobalMiddlewareFactory>();
        services.AddSingleton<IGlobalMiddlewareFactory, HeaderGlobalMiddlewareFactory>();
        services.AddSingleton<GlobalHeadersManager>();

        return services;
    }
}
