using Arbiter.Application.Configuration;
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
        services.AddSingleton<TransactionIdProvider>();
        services.AddSingleton<GlobalMiddlewareChain>();
        services.AddSingleton<AltSvcService>();

        services.AddScoped<MiddlewareChainDelegateOrchestrator>();
        services.AddScoped<SiteOrchestrator>();

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

    public static void AddGlobalMiddleware<T>(this IServiceCollection services) where T : class, IGlobalMiddleware
        => services.AddSingleton(new GlobalMiddlewareDescriptor(typeof(T)));

    public static void ConfigureGlobalMiddlewareChain(GlobalMiddlewareChain chain)
    {
        chain.Add(next => new ExceptionCatcherGlobalMiddleware(next).Handle);
        chain.Add(next => new NullSiteGlobalMiddleware(next).Handle);
    }
}
