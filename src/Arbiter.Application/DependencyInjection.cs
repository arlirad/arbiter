using Arbiter.Application.Configuration;
using Arbiter.Application.Handlers;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Application.Mappers;
using Arbiter.Application.Middleware;
using Arbiter.Application.Orchestrators;
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

        services.AddScoped<MiddlewareChainDelegateOrchestrator>();
        services.AddScoped<SiteOrchestrator>();

        services.AddSingleton(GlobalMiddlewareInjection.GetHandleDelegate);

        services.AddTransient<Core.Interfaces.HandleDelegate>(sp => {
            var factory = sp.GetRequiredService<MiddlewareChainDelegateOrchestrator>();
            return factory.GetNext();
        });
    }

    public static void AddTransport<TAcceptor, TConfig>(this IServiceCollection services, string key)
        where TAcceptor : class, IAcceptor
        where TConfig : class
    {
        services.AddKeyedSingleton<IAcceptor, TAcceptor>(key);
        services.AddSingleton(new TransportDescriptor(key, typeof(TAcceptor), typeof(TConfig)));
    }

    public static void AddApplicationGlobalMiddleware(this IServiceCollection services)
    {
        services.AddGlobalMiddleware<ServerHeaderGlobalMiddleware>();
        services.AddGlobalMiddleware<ExceptionCatcherGlobalMiddleware>();
        services.AddGlobalMiddleware<NullSiteGlobalMiddleware>();
    }
}