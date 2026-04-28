using Arbiter.Application.Managers;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;

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

    private static SiteManager CreateManager(out Site site)
    {
        var manager = new SiteManager(new ServiceProviderStub());

        site = new Site("/tmp", [new Uri("http://example.com:8080")], [], [], _ => Task.CompletedTask);

        typeof(SiteManager)
            .GetField("_sites", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(manager, new Dictionary<string, Site> { ["test"] = site });

        return manager;
    }

    private sealed class ServiceProviderStub : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
