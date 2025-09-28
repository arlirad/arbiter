using Arbiter.Helpers;
using Arbiter.Middleware;
using Arbiter.Models;
using Arbiter.Models.Config.Sites;
using Arbiter.Services;
using Arbiter.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Factories;


internal class SiteFactory(
    MiddlewareFactory middlewareFactory,
    WorkerFactory workerFactory,
    ConfigManager configManager,
    IServiceScopeFactory scopeFactory
)
{
    public async Task<Site> Create(SiteConfigModel siteConfig)
    {
        var scope = scopeFactory.CreateScope();
        
        siteConfig.Middleware ??= [];
        siteConfig.Workers ??= [];

        var middlewares = siteConfig.Middleware
            .Select<SiteComponentConfigModel, (IMiddleware Instance, IConfiguration Config)>(m => 
                (Instance: middlewareFactory.Create(m.Name!, scope), 
                    ConfigMerger.Merge(configManager.GetDefaultMiddlewareConfig(m.Name!), m.Config)))
            .ToList();
        
        var workers = siteConfig.Workers
            .Select<SiteComponentConfigModel, (IWorker Instance, IConfiguration Config)>(w => 
                (Instance: workerFactory.Create(w.Name!, scope), w.Config))
            .ToList();

        var site = new Site(
            siteConfig.Path!, 
            siteConfig.Bindings!,
            middlewares.Select(m => m.Instance), 
            workers.Select(w => w.Instance)
        );

        foreach (var middleware in middlewares)
        {
            await middleware.Instance.Configure(site, middleware.Config!);
        }

        foreach (var worker in workers)
        {
            await worker.Instance.Configure(site, worker.Config!);
        }

        return site;
    }
}