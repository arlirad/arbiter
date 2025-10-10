using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Application.Orchestrators;

internal class SiteOrchestrator(IServiceProvider serviceProvider, IConfigManager configManager)
{
    public async Task<Site> Orchestrate(SiteConfig siteConfig)
    {
        siteConfig.Workers ??= [];

        var middlewareChain = CreateMiddlewareChain(siteConfig);
        var workers = siteConfig.Workers
            .Select<SiteComponentConfig, (IWorker Instance, IConfiguration Config)>(w =>
                (Instance: InstanceWorker(w.Name!), w.Config))
            .ToList();

        var handleDelegate = (Arbiter.Domain.Interfaces.HandleDelegate)(middlewareChain.Count > 0
            ? middlewareChain.First().Instance.Handle
            : LastHandleDelegate);

        var site = new Site(
            siteConfig.Path!,
            siteConfig.Bindings!,
            middlewareChain.Select(m => m.Instance),
            workers.Select(w => w.Instance),
            handleDelegate
        );

        foreach (var middleware in middlewareChain)
        {
            await middleware.Instance.Configure(site, middleware.Config!);
        }

        foreach (var worker in workers)
        {
            await worker.Instance.Configure(site, worker.Config!);
        }

        return site;
    }

    private List<(IMiddleware Instance, IConfiguration Config)> CreateMiddlewareChain(SiteConfig siteConfig)
    {
        if (siteConfig.Middleware is null)
            return [];

        var chainOrchestrator = serviceProvider.GetRequiredService<MiddlewareChainDelegateOrchestrator>();

        chainOrchestrator.SetNext(LastHandleDelegate);

        var middlewareConfigs = new List<SiteComponentConfig>(siteConfig.Middleware);

        // We have to reverse the config list so we know what handler comes next.
        middlewareConfigs.Reverse();

        var middlewareChainReversed = middlewareConfigs
            .Select<SiteComponentConfig, (IMiddleware Instance, IConfiguration Config)>(m =>
            {
                var middleware = (Instance: InstanceMiddleware(m.Name!),
                    MergeConfigs(configManager.GetDefaultMiddlewareConfig(m.Name!), m.Config));

                chainOrchestrator.SetNext(middleware.Instance.Handle);

                return middleware;
            })
            .ToList();

        middlewareChainReversed.Reverse();

        return middlewareChainReversed;
    }

    private static Task LastHandleDelegate(Context _)
    {
        return Task.CompletedTask;
    }

    private static IConfiguration MergeConfigs(params IConfiguration?[] configs)
    {
        var builder = new ConfigurationBuilder();

        foreach (var c in configs)
        {
            if (c is not null)
                builder.AddConfiguration(c);
        }

        return builder.Build();
    }

    private IMiddleware InstanceMiddleware(string name)
    {
        return serviceProvider.GetKeyedService<IMiddleware>(name)
            ?? throw new Exception($"Middleware '{name}' not found");
    }

    private IWorker InstanceWorker(string name)
    {
        return serviceProvider.GetKeyedService<IWorker>(name)
            ?? throw new Exception($"Worker '{name}' not found");
    }
}