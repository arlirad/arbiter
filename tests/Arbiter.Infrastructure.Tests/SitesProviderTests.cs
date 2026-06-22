using System.Reactive;
using System.Reactive.Linq;
using Arbiter.Application.Configuration;
using Arbiter.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;
using ConfigurationProvider = Arbiter.Configuration.ConfigurationProvider;

namespace Arbiter.Infrastructure.Tests;

[TestFixture]
public class SitesProviderTests
{
    private static readonly ISerializer Yaml = new SerializerBuilder().Build();

    private string? _tempDir;
    private SitesProvider? _provider;

    [TearDown]
    public void TearDown()
    {
        _provider?.Dispose();

        if (_tempDir is not null && Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static void WriteSite(string dir, string name, object config) => File.WriteAllText(Path.Join(dir, $"{name}.yaml"), Yaml.Serialize(config));

    private static ConfigurationProvider CreateConfigProvider(Dictionary<string, string?>? data = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data ?? [])
            .Build();

        return new ConfigurationProvider(config);
    }

    private List<Dictionary<string, SiteConfig>> Setup(string tempDir, Dictionary<string, string?>? mainData = null)
    {
        var configProvider = CreateConfigProvider(mainData);
        _provider = new SitesProvider(configProvider, tempDir);

        var emissions = new List<Dictionary<string, SiteConfig>>();
        _provider.ObserveSites().Subscribe(emissions.Add);

        return emissions;
    }

    [Test]
    public void Adding_site_yaml_emits_merged_sites()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arbiter-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var emissions = Setup(_tempDir, new Dictionary<string, string?> {
            ["Sites:main-site:Path"] = "/var/www/main",
        });

        Thread.Sleep(200);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(emissions, Has.Count.EqualTo(1));
            Assert.That(emissions[0], Contains.Key("main-site"));
            Assert.That(emissions[0], Does.Not.ContainKey("my-api"));
        }

        WriteSite(_tempDir, "my-api", new {
            bindings = new[] { "http://api.example.com" },
            middleware = new[] { new { name = "proxy", config = new { target = "http://localhost:5000" } } },
        });

        Thread.Sleep(1000);

        var latest = emissions[^1];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(latest, Contains.Key("main-site"));
            Assert.That(latest, Contains.Key("my-api"));
            Assert.That(latest["my-api"].Bindings![0].AbsoluteUri, Is.EqualTo("http://api.example.com/"));
        }
    }

    [Test]
    public void Changing_site_yaml_emits_updated_config()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arbiter-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        WriteSite(_tempDir, "dynamic", new {
            path = "/var/www/v1",
            bindings = new[] { "http://dynamic.example.com" },
        });

        var emissions = Setup(_tempDir);

        Thread.Sleep(200);

        Assert.That(emissions[^1]["dynamic"].Path, Is.EqualTo("/var/www/v1"));

        WriteSite(_tempDir, "dynamic", new {
            path = "/var/www/v2",
            bindings = new[] { "http://dynamic.example.com" },
        });

        Thread.Sleep(1000);

        Assert.That(emissions[^1]["dynamic"].Path, Is.EqualTo("/var/www/v2"));
    }

    [Test]
    public void Removing_site_yaml_emits_without_removed_site()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arbiter-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        WriteSite(_tempDir, "removable", new {
            bindings = new[] { "http://remove.example.com" },
        });

        var emissions = Setup(_tempDir);

        Thread.Sleep(200);

        Assert.That(emissions[^1], Contains.Key("removable"));

        File.Delete(Path.Join(_tempDir, "removable.yaml"));

        Thread.Sleep(1000);

        Assert.That(emissions[^1], Does.Not.ContainKey("removable"));
    }

    [Test]
    public void Malformed_yaml_is_skipped_without_crash()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arbiter-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        File.WriteAllText(Path.Join(_tempDir, "broken.yaml"), "{{{ not yaml");
        WriteSite(_tempDir, "good", new {
            bindings = new[] { "http://good.example.com" }
        });

        var emissions = Setup(_tempDir);

        Thread.Sleep(200);

        var latest = emissions[^1];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(latest, Does.Not.ContainKey("broken"));
            Assert.That(latest, Contains.Key("good"));
        }
    }

    [Test]
    public void Directory_site_overrides_main_config_with_same_key()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arbiter-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var emissions = Setup(_tempDir, new Dictionary<string, string?> {
            ["Sites:shared:Path"] = "/var/www/original",
        });

        Thread.Sleep(200);

        Assert.That(emissions[^1]["shared"].Path, Is.EqualTo("/var/www/original"));

        WriteSite(_tempDir, "shared", new {
            path = "/var/www/overridden"
        });

        Thread.Sleep(1000);

        Assert.That(emissions[^1]["shared"].Path, Is.EqualTo("/var/www/overridden"));
    }

    [Test]
    public void No_main_sites_still_emits_directory_sites()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arbiter-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        WriteSite(_tempDir, "standalone", new {
            bindings = new[] { "http://standalone.example.com" }
        });

        var emissions = Setup(_tempDir);

        Thread.Sleep(200);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(emissions, Is.Not.Empty);
            Assert.That(emissions[^1], Contains.Key("standalone"));
        }
    }
}
