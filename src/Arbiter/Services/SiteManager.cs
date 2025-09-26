using Arbiter.Factories;
using Arbiter.Middleware;
using Arbiter.Models;
using Arbiter.Models.Config;
using Arbiter.Models.Config.Sites;
using Arbiter.Workers;
using Serilog;

namespace Arbiter.Services;

internal class SiteManager(
    SiteFactory siteFactory
)
{
    private Dictionary<string, Site> _sites = new();

    public Site? Find(Uri uri)
    {
        return _sites.FirstOrDefault(s => 
            s.Value.Bindings.Any(b => b.Host.Equals(uri.Host) && b.Port == uri.Port)
        ).Value;
    }
    
    public async Task Update(ConfigModel config)
    {
        if (config.Sites is null)
            return;

        await CreateSites(config);
        // await RecreateSites(config);
        await PruneSites(config);
    }

    private async Task CreateSites(ConfigModel config)
    {
        if (config.Sites is null)
            throw new InvalidOperationException("config.Sites cannot be null");

        var sitesToCreate = config.Sites
            .Where(s => !_sites.ContainsKey(s.Key))
            .ToList();

        foreach (var siteToCreate in sitesToCreate)
        {
            var site = await siteFactory.Create(siteToCreate.Value);
            await site.Start();
        
            Log.Information("Started site '{Key}'", siteToCreate.Key);
            
            _sites[siteToCreate.Key] = site;
        }
    }

    private async Task RecreateSites(ConfigModel config)
    {
        if (config.Sites is null)
            throw new InvalidOperationException("config.Sites cannot be null");
        
        throw new NotImplementedException();
    }

    private async Task PruneSites(ConfigModel config)
    {
        if (config.Sites is null)
            throw new InvalidOperationException("config.Sites cannot be null");

        var sitesToPrune = _sites
            .Where(site => !config.Sites.ContainsKey(site.Key))
            .Select(kvp => kvp.Key)
            .ToList();

        var stopTasks = new List<Task>();

        foreach (var siteKey in sitesToPrune)
        {
            var site = _sites[siteKey];
            _sites.Remove(siteKey);
            
            stopTasks.Add(site.Stop());
        }

        await Task.WhenAll(stopTasks);
    }
}