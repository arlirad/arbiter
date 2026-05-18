using Arbiter.Application.Configuration;
using Arbiter.Application.Orchestrators;
using Arbiter.Configuration;
using Arbiter.Core.Aggregates;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Arbiter.Application.Managers;

internal class SiteManager(
    IServiceProvider serviceProvider
) : IAsyncConfigurable<Dictionary<string, SiteConfig>>, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "site");
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<Site, SiteConfig> _siteConfigs = [];
    private readonly Dictionary<string, Site> _sites = [];
    private Dictionary<(string host, int port), Site> _bindingIndex = [];

    public async ValueTask ReconfigureAsync(Dictionary<string, SiteConfig> configuration)
    {
        if (configuration is null)
            throw new InvalidOperationException("config.Sites cannot be null");

        List<KeyValuePair<string, SiteConfig>> toCreate;
        List<string> toRecreate;
        List<string> toPrune;

        await _lock.WaitAsync();
        try
        {
            var existingKeys = new HashSet<string>(_sites.Keys);
            var configKeys = new HashSet<string>(configuration.Keys);

            toCreate = [.. configuration.Where(kvp => !existingKeys.Contains(kvp.Key))];

            toRecreate = [.. _sites
                .Where(kvp => configuration.TryGetValue(kvp.Key, out var newCfg)
                    && _siteConfigs.TryGetValue(kvp.Value, out var oldCfg)
                    && !oldCfg.Equals(newCfg))
                .Select(kvp => kvp.Key)];

            toPrune = [.. existingKeys.Except(configKeys)];
        }
        finally
        {
            _lock.Release();
        }

        var stagedSites = new Dictionary<string, Site>();
        var sitesToStop = new Dictionary<string, Site>();

        await _lock.WaitAsync();
        try
        {
            foreach (var key in toRecreate)
            {
                if (_sites.TryGetValue(key, out var site))
                {
                    sitesToStop[key] = site;
                    _sites.Remove(key);
                    _siteConfigs.Remove(site);
                }
            }

            foreach (var key in toPrune)
            {
                if (_sites.TryGetValue(key, out var site))
                {
                    sitesToStop[key] = site;
                    _sites.Remove(key);
                    _siteConfigs.Remove(site);
                }
            }

            stagedSites = new Dictionary<string, Site>(_sites);
        }
        finally
        {
            _lock.Release();
        }

        foreach (var kvp in toCreate)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<SiteOrchestrator>();
            var site = await factory.Orchestrate(kvp.Value);
            await site.Start();
            Log.Information("Started site '{Key}'", kvp.Key);
            stagedSites[kvp.Key] = site;
        }

        foreach (var key in toRecreate.Where(configuration.ContainsKey))
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<SiteOrchestrator>();
            var site = await factory.Orchestrate(configuration[key]);
            await site.Start();
            Log.Information("Reloaded site '{Key}'", key);
            stagedSites[key] = site;
        }

        await _lock.WaitAsync();
        try
        {
            _sites.Clear();
            foreach (var kvp in stagedSites)
                _sites[kvp.Key] = kvp.Value;

            _siteConfigs.Clear();
            foreach (var kvp in stagedSites)
                _siteConfigs[kvp.Value] = configuration[kvp.Key];

            _bindingIndex = BuildBindingIndex(_sites);
        }
        finally
        {
            _lock.Release();
        }

        foreach (var kvp in sitesToStop)
        {
            await kvp.Value.Stop();

            if (!stagedSites.ContainsKey(kvp.Key))
                Log.Information("Removed site '{Key}'", kvp.Key);
        }
    }

    public Site? Find(string? authority, int port)
    {
        var host = StripPort(authority) ?? "*";

        return _bindingIndex.TryGetValue((host, port), out var site)
            ? site
            : _bindingIndex.TryGetValue(("*", port), out var wildcardSite) ? wildcardSite : null;
    }

    public Site? Find(string? authority)
    {
        var host = StripPort(authority) ?? "*";

        foreach (var key in _bindingIndex.Keys)
        {
            if (key.host == host)
                return _bindingIndex[key];
        }

        foreach (var key in _bindingIndex.Keys)
        {
            if (key.host == "*")
                return _bindingIndex[key];
        }

        return null;
    }

    private static Dictionary<(string host, int port), Site> BuildBindingIndex(Dictionary<string, Site> sites)
    {
        var index = new Dictionary<(string, int), Site>();

        foreach (var site in sites.Values)
        {
            foreach (var binding in site.Bindings ?? [])
            {
                var host = binding.Host;
                var port = binding.Port;

                if (!index.ContainsKey((host, port)))
                    index[(host, port)] = site;
            }
        }

        return index;
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

    public void Dispose() => _lock.Dispose();
}