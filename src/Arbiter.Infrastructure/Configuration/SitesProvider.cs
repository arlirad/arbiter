using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;
using Microsoft.Extensions.Configuration;
using Serilog;
using CoreConfigurationProvider = Arbiter.Configuration.ConfigurationProvider;

namespace Arbiter.Infrastructure.Configuration;

internal sealed class SitesProvider : ISitesProvider, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "sites");
    private readonly string _sitesDirectory;
    private readonly FileSystemWatcher _watcher;
    private readonly Subject<Unit> _dirChanged = new();
    private readonly IObservable<Dictionary<string, SiteConfig>> _combined;

    public SitesProvider(CoreConfigurationProvider configProvider, string sitesDirectory)
    {
        _sitesDirectory = sitesDirectory;

        Directory.CreateDirectory(sitesDirectory);

        var mainSites = configProvider
            .Observe<Dictionary<string, SiteConfig>>("Sites")
            .StartWith(new Dictionary<string, SiteConfig>());

        var dirSites = _dirChanged
            .Throttle(TimeSpan.FromMilliseconds(500))
            .StartWith(Unit.Default)
            .Select(_ => LoadDirectorySites());

        _combined = Observable.CombineLatest(mainSites, dirSites, MergeSites);

        _watcher = new FileSystemWatcher(sitesDirectory) {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Filters.Add("*.yaml");
        _watcher.Filters.Add("*.yml");

        _watcher.Changed += (_, _) => _dirChanged.OnNext(Unit.Default);
        _watcher.Created += (_, _) => _dirChanged.OnNext(Unit.Default);
        _watcher.Deleted += (_, _) => _dirChanged.OnNext(Unit.Default);
        _watcher.Renamed += (_, _) => _dirChanged.OnNext(Unit.Default);
        _watcher.Error += (_, e) => Log.Warning(e.GetException(), "File system watcher error on {Directory}", sitesDirectory);
    }

    public IObservable<Dictionary<string, SiteConfig>> ObserveSites() => _combined;

    private Dictionary<string, SiteConfig> LoadDirectorySites()
    {
        var result = new Dictionary<string, SiteConfig>();

        if (!Directory.Exists(_sitesDirectory))
            return result;

        foreach (var pattern in new[] { "*.yaml", "*.yml" })
        {
            foreach (var file in Directory.EnumerateFiles(_sitesDirectory, pattern))
            {
                var key = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var cfg = new ConfigurationBuilder()
                        .AddYamlFile(file, optional: false, reloadOnChange: false)
                        .Build();

                    var site = cfg.Get<SiteConfig>();
                    if (site is null)
                    {
                        Log.Warning("Site config '{File}' produced null — skipping", file);
                        continue;
                    }

                    result[key] = site;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load site config from '{File}' — skipping", file);
                }
            }
        }

        return result;
    }

    private Dictionary<string, SiteConfig> MergeSites(
        Dictionary<string, SiteConfig>? main,
        Dictionary<string, SiteConfig>? dir)
    {
        var merged = new Dictionary<string, SiteConfig>();

        if (main is not null)
            foreach (var kvp in main)
                merged[kvp.Key] = kvp.Value;

        if (dir is not null)
            foreach (var kvp in dir)
            {
                if (merged.ContainsKey(kvp.Key))
                    Log.Warning("Site '{Key}' from {Directory} overrides main config", kvp.Key, _sitesDirectory);
                merged[kvp.Key] = kvp.Value;
            }

        return merged;
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _dirChanged.Dispose();
    }
}