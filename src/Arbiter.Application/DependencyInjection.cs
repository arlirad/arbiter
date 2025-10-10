using Arbiter.Application.Configuration;
using Arbiter.Application.Handlers;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Application.Mappers;
using Arbiter.Application.Middleware;
using Arbiter.Application.Orchestrators;
using Arbiter.Domain.Factories;
using Arbiter.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Application;

public static class DependencyInjection
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IServer, Server>();

        services.AddSingleton<ICertificateManager, CertificateManager>();
        services.AddSingleton<IConfigurator, SiteManagerConfigurator>();
        services.AddSingleton<IContextFactory, ContextFactory>();
        services.AddSingleton<TransactionHandler>();
        services.AddSingleton<ContextMapper>();
        services.AddSingleton<SiteManager>();

        services.AddScoped<MiddlewareChainDelegateOrchestrator>();
        services.AddScoped<SiteOrchestrator>();

        services.AddSingleton(GlobalMiddlewareInjection.GetHandleDelegate);

        services.AddTransient<Domain.Interfaces.HandleDelegate>(sp =>
        {
            var factory = sp.GetRequiredService<MiddlewareChainDelegateOrchestrator>();
            return factory.GetNext();
        });
    }

    public static void AddApplicationGlobalMiddleware(this IServiceCollection services)
    {
        services.AddGlobalMiddleware<NullSiteGlobalMiddleware>();
    }
}