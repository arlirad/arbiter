using Arbiter.Application.Configuration;
using Arbiter.Application.Orchestrators;
using Arbiter.Domain.Aggregates;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Arbiter.Application.Managers;

internal class SiteManager(
    IServiceProvider serviceProvider
)
{
    private readonly Dictionary<string, Site> _sites = new();

    public Site? Find(string? host, int port)
    {
        host ??= "*";

        return _sites.FirstOrDefault(s =>
            s.Value.Bindings.Any(b => b.Host.Equals(host) && b.Port.Equals(port))
        ).Value;
    }

    public async Task Update(Dictionary<string, SiteConfig> sites)
    {
        await CreateSites(sites);
        // await RecreateSites(config);
        await PruneSites(sites);
    }

    private async Task CreateSites(Dictionary<string, SiteConfig> sites)
    {
        if (sites is null)
            throw new InvalidOperationException("config.Sites cannot be null");

        var sitesToCreate = sites
            .Where(s => !_sites.ContainsKey(s.Key))
            .ToList();

        foreach (var siteToCreate in sitesToCreate)
        {
            await using var scope = serviceProvider.CreateAsyncScope();

            var factory = scope.ServiceProvider.GetRequiredService<SiteOrchestrator>();
            var site = await factory.Orchestrate(siteToCreate.Value);

            await site.Start();

            Log.Information("Started site '{Key}'", siteToCreate.Key);

            _sites[siteToCreate.Key] = site;
        }
    }

    private async Task RecreateSites(Dictionary<string, SiteConfig> sites)
    {
        if (sites is null)
            throw new InvalidOperationException("config.Sites cannot be null");

        throw new NotImplementedException();
    }

    private async Task PruneSites(Dictionary<string, SiteConfig> sites)
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