using System.Reflection;
using Arbiter.Application.Configuration;
using Arbiter.Application.Managers;
using Arbiter.Core.Aggregates;

namespace Arbiter.Application.Tests;

public class SiteManagerTests
{
    [Test]
    public void Find_strips_port_from_authority()
    {
        var manager = CreateManager(out var site);

        var result = manager.Find("example.com:8080", 8080);

        Assert.That(result, Is.SameAs(site));
    }

    [Test]
    public void Find_without_port_strips_port_from_authority()
    {
        var manager = CreateManager(out var site);

        var result = manager.Find("example.com:8080");

        Assert.That(result, Is.SameAs(site));
    }

    [Test]
    public void Find_returns_null_when_port_mismatches()
    {
        var manager = CreateManager(out _);

        var result = manager.Find("example.com:8080", 9090);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Find_returns_null_for_unknown_host()
    {
        var manager = CreateManager(out _);

        var result = manager.Find("unknown.com:8080", 8080);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Find_with_null_authority_uses_wildcard()
    {
        var manager = CreateManager(out _);

        var result = manager.Find(null, 8080);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Find_handles_authority_without_port()
    {
        var manager = CreateManager(out var site);

        var result = manager.Find("example.com");

        Assert.That(result, Is.SameAs(site));
    }

    [Test]
    public async Task ReconfigureAsync_prunes_removed_sites()
    {
        var manager = new SiteManager(new ServiceProviderStub());

        var site = new Site([new Uri("http://example.com:8080")], [], [], _ => Task.CompletedTask);

        var sitesField = typeof(SiteManager).GetField("_sites", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var configsField = typeof(SiteManager).GetField("_siteConfigs", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var indexField = typeof(SiteManager).GetField("_bindingIndex", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var sites = new Dictionary<string, Site> {
            ["test"] = site,
        };

        sitesField.SetValue(manager, sites);
        configsField.SetValue(manager, new Dictionary<Site, SiteConfig> {
            [site] = new(),
        });

        indexField.SetValue(manager, BuildIndex(sites));

        await manager.ReconfigureAsync([]);

        var sitesAfter = (Dictionary<string, Site>)sitesField.GetValue(manager)!;
        var configsAfter = (Dictionary<Site, SiteConfig>)configsField.GetValue(manager)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sitesAfter, Is.Empty);
            Assert.That(configsAfter, Is.Empty);
        }
    }

    [Test]
    public void ReconfigureAsync_with_null_configuration_throws()
    {
        var manager = new SiteManager(new ServiceProviderStub());

        Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.ReconfigureAsync(null!));
    }

    private static SiteManager CreateManager(out Site site)
    {
        var manager = new SiteManager(new ServiceProviderStub());

        site = new Site([new Uri("http://example.com:8080")], [], [], _ => Task.CompletedTask);

        var sites = new Dictionary<string, Site> {
            ["test"] = site,
        };

        typeof(SiteManager)
            .GetField("_sites", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(manager, sites);

        typeof(SiteManager)
            .GetField("_bindingIndex", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(manager, BuildIndex(sites));

        return manager;
    }

    private static Dictionary<(string host, int port), Site> BuildIndex(Dictionary<string, Site> sites)
    {
        var index = new Dictionary<(string, int), Site>();

        foreach (var s in sites.Values)
        {
            foreach (var binding in s.Bindings ?? [])
            {
                if (!index.ContainsKey((binding.Host, binding.Port)))
                    index[(binding.Host, binding.Port)] = s;
            }
        }

        return index;
    }

    private sealed class ServiceProviderStub : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
