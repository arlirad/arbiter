using Arbiter.Factories;
using Arbiter.Middleware;
using Arbiter.Models;
using Arbiter.Models.Config;
using Arbiter.Models.Config.Sites;
using Arbiter.Workers;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Arbiter.Services;

internal class SiteManager(
    IServiceProvider serviceProvider
)
{
    private Dictionary<string, Site> _sites = new();

    public Site? Find(Uri uri)
    {
        return _sites.FirstOrDefault(s =>
            s.Value.Bindings.Any(b => b.Host.Equals(uri.Host) && b.Port == uri.Port)
        ).Value;
    }

    public async Task Update(Dictionary<string, SiteConfigModel> sites)
    {
        await CreateSites(sites);
        // await RecreateSites(config);
        await PruneSites(sites);
    }

    private async Task CreateSites(Dictionary<string, SiteConfigModel> sites)
    {
        if (sites is null)
            throw new InvalidOperationException("config.Sites cannot be null");

        var sitesToCreate = sites
            .Where(s => !_sites.ContainsKey(s.Key))
            .ToList();

        foreach (var siteToCreate in sitesToCreate)
        {
            await using var scope = serviceProvider.CreateAsyncScope();

            var factory = scope.ServiceProvider.GetRequiredService<SiteFactory>();
            var site = await factory.Create(siteToCreate.Value);

            await site.Start();

            Log.Information("Started site '{Key}'", siteToCreate.Key);

            _sites[siteToCreate.Key] = site;
        }
    }

    private async Task RecreateSites(Dictionary<string, SiteConfigModel> sites)
    {
        if (sites is null)
            throw new InvalidOperationException("config.Sites cannot be null");

        throw new NotImplementedException();
    }

    private async Task PruneSites(Dictionary<string, SiteConfigModel> sites)
    {
        if (sites is null)
            throw new InvalidOperationException("config.Sites cannot be null");

        var sitesToPrune = _sites
            .Where(site => !sites.ContainsKey(site.Key))
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