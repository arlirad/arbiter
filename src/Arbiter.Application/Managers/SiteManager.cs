using System.Threading;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Orchestrators;
using Arbiter.Core.Aggregates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Serilog;

namespace Arbiter.Application.Managers;

internal class SiteManager(
    IServiceProvider serviceProvider
) : IAsyncConfigurable
{
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly Dictionary<string, SiteConfig> _siteConfigs = [];
    private readonly Dictionary<string, Site> _sites = [];
    private ConfigurationScope? _scope;

    public async ValueTask Bind(IConfiguration configuration)
    {
        _scope = new ConfigurationScope(configuration, "Sites");
        await UpdateSites();
        ChangeToken.OnChange(_scope.GetReloadToken, () => _ = UpdateSitesAsync());
    }

    private async Task UpdateSitesAsync()
    {
        if (!await _reloadLock.WaitAsync(0))
            return;

        try
        {
            await UpdateSites();
        }
        catch (Exception e)
        {
            Log.Error("Failed to reload sites: {Exception}", e);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public Site? Find(string? authority, int port)
    {
        var host = StripPort(authority) ?? "*";

        return _sites.FirstOrDefault(s =>
            s.Value.Bindings.Any(b => b.Host.Equals(host) && b.Port.Equals(port))
        ).Value;
    }

    public Site? Find(string? authority)
    {
        var host = StripPort(authority) ?? "*";

        return _sites.FirstOrDefault(s =>
            s.Value.Bindings.Any(b => b.Host.Equals(host))
        ).Value;
    }

    private static string? StripPort(string? authority)
    {
        if (authority is null)
            return null;

        var colon = authority.LastIndexOf(':');

        if (colon <= 0)
            return authority;

        if (authority.StartsWith('['))
        {
            var bracket = authority.IndexOf(']', colon);

            if (bracket == -1)
                return authority[..colon];
        }

        return authority[..colon];
    }

    private async Task UpdateSites()
    {
        if (_scope is null)
            return;

        var sites = _scope.GetSection("Sites").Get<Dictionary<string, SiteConfig>>();

        if (sites is null)
            return;

        await CreateSites(sites);
        await RecreateSites(sites);
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
            _siteConfigs[siteToCreate.Key] = siteToCreate.Value;
        }
    }

    private async Task RecreateSites(Dictionary<string, SiteConfig> sites)
    {
        if (sites is null)
            throw new InvalidOperationException("config.Sites cannot be null");

        var sitesToRecreate = _siteConfigs
            .Where(kvp => sites.TryGetValue(kvp.Key, out var newConfig) &&
                !kvp.Value.Equals(newConfig))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var siteKey in sitesToRecreate)
        {
            var oldSite = _sites[siteKey];
            var newConfig = sites[siteKey];

            await oldSite.Stop();

            Log.Information("Recreating site '{Key}'", siteKey);

            await using var scope = serviceProvider.CreateAsyncScope();

            var factory = scope.ServiceProvider.GetRequiredService<SiteOrchestrator>();
            var newSite = await factory.Orchestrate(newConfig);

            await newSite.Start();

            _sites[siteKey] = newSite;
            _siteConfigs[siteKey] = newConfig;

            Log.Information("Recreated site '{Key}'", siteKey);
        }
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
            _siteConfigs.Remove(siteKey);

            stopTasks.Add(site.Stop());
        }

        await Task.WhenAll(stopTasks);
    }
}