using Arbiter.Application.Handlers;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Application.Mappers;
using Arbiter.Application.Middleware;
using Arbiter.Application.Orchestrators;
using Arbiter.Application.Services;
using Arbiter.Configuration;
using Arbiter.Core.Factories;
using Arbiter.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Application;

public static class DependencyInjection
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ConfigurationProvider>();
        services.AddSingleton<IServer, Server>();

        services.AddSingleton<ICertificateManager, CertificateManager>();
        services.AddSingleton<IContextFactory, ContextFactory>();
        services.AddSingleton<TransactionHandler>();
        services.AddSingleton<ContextMapper>();
        services.AddSingleton<SiteManager>();
        services.AddSingleton<TransportManager>();
        services.AddSingleton<GlobalMiddlewareChain>();
        services.AddSingleton<AltSvcService>();
        services.AddSingleton<IProtocolFactory, ProtocolService>();
        services.AddSingleton<IGlobalMiddlewareFactory, CoreGlobalMiddlewareFactory>();

        services.AddScoped<MiddlewareChainDelegateOrchestrator>();
        services.AddScoped<SiteOrchestrator>();

        services.AddTransient<HandleDelegate>(sp => {
            var factory = sp.GetRequiredService<MiddlewareChainDelegateOrchestrator>();

            return factory.GetNext();
        });
    }
}
