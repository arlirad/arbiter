using Arbiter.Helpers;
using Arbiter.Middleware;
using Arbiter.Models;
using Arbiter.Models.Config.Sites;
using Arbiter.Models.Network;
using Arbiter.Services;
using Arbiter.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Factories;

internal class SiteFactory(
    MiddlewareFactory middlewareFactory,
    WorkerFactory workerFactory,
    ConfigManager configManager,
    MiddlewareChainDelegateFactory middlewareChainDelegateFactory
)
{
    public async Task<Site> Create(SiteConfigModel siteConfig)
    {
        siteConfig.Workers ??= [];

        var middlewareChain = CreateMiddlewareChain(siteConfig);
        var workers = siteConfig.Workers
            .Select<SiteComponentConfigModel, (IWorker Instance, IConfiguration Config)>(w =>
                (Instance: workerFactory.Create(w.Name!), w.Config))
            .ToList();

        var site = new Site(
            siteConfig.Path!,
            siteConfig.Bindings!,
            middlewareChain.Select(m => m.Instance),
            workers.Select(w => w.Instance)
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

    private List<(IMiddleware Instance, IConfiguration Config)> CreateMiddlewareChain(SiteConfigModel siteConfig)
    {
        if (siteConfig.Middleware is null)
            return [];

        middlewareChainDelegateFactory.SetNext(LastHandleDelegate);

        var middlewareConfigs = new List<SiteComponentConfigModel>(siteConfig.Middleware);

        // We have to reverse the config list so we know what handler comes next.
        middlewareConfigs.Reverse();

        var middlewareChainReversed = middlewareConfigs
            .Select<SiteComponentConfigModel, (IMiddleware Instance, IConfiguration Config)>(m =>
            {
                var middleware = (Instance: middlewareFactory.Create(m.Name!),
                    ConfigMerger.Merge(configManager.GetDefaultMiddlewareConfig(m.Name!), m.Config));

                middlewareChainDelegateFactory.SetNext(middleware.Instance.Handle);

                return middleware;
            })
            .ToList();

        middlewareChainReversed.Reverse();

        return middlewareChainReversed;
    }

    private static Task LastHandleDelegate(HttpContext _)
    {
        return Task.CompletedTask;
    }
}