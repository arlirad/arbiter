using Arbiter.Application.Configuration;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Tests;

[TestFixture]
public class SiteComponentConfigTests
{
    [TearDown]
    public void TearDown()
    {
        if (_tempFile != null && File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    private string? _tempFile;

    [Test]
    public void Equals_returns_true_for_same_name_and_same_config()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["S:Key"] = "value",
            })
            .Build();

        var a = new SiteComponentConfig {
            Name = "proxy",
            Config = config.GetSection("S"),
        };

        var b = new SiteComponentConfig {
            Name = "proxy",
            Config = config.GetSection("S"),
        };

        Assert.That(a.Equals(b), Is.True);
    }

    [Test]
    public void Equals_returns_false_for_same_name_different_config()
    {
        var configA = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["S:Key"] = "value-a",
            })
            .Build();

        var configB = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["S:Key"] = "value-b",
            })
            .Build();

        var a = new SiteComponentConfig {
            Name = "proxy",
            Config = configA.GetSection("S"),
        };

        var b = new SiteComponentConfig {
            Name = "proxy",
            Config = configB.GetSection("S"),
        };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_returns_false_for_different_name_same_config()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["S:Key"] = "value",
            })
            .Build();

        var section = config.GetSection("S");
        var a = new SiteComponentConfig {
            Name = "proxy",
            Config = section,
        };

        var b = new SiteComponentConfig {
            Name = "auth",
            Config = section,
        };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_returns_false_when_one_has_null_config_and_other_has_values()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["S:Key"] = "value",
            })
            .Build();

        var a = new SiteComponentConfig {
            Name = "proxy",
            Config = config.GetSection("S"),
        };

        var b = new SiteComponentConfig {
            Name = "proxy",
            Config = null,
        };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_returns_true_when_both_have_null_config()
    {
        var a = new SiteComponentConfig {
            Name = "proxy",
            Config = null,
        };

        var b = new SiteComponentConfig {
            Name = "proxy",
            Config = null,
        };

        Assert.That(a.Equals(b), Is.True);
    }

    [Test]
    public void GetHashCode_is_consistent_with_Equals()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["S:Key"] = "value",
            })
            .Build();

        var a = new SiteComponentConfig {
            Name = "proxy",
            Config = config.GetSection("S"),
        };

        var b = new SiteComponentConfig {
            Name = "proxy",
            Config = config.GetSection("S"),
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }
    }

    [Test]
    public void Equals_returns_false_after_reload_old_snapshot_preserved()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, """{"S":{"Target":"http://backend:5000"}}""");

        var config = new ConfigurationBuilder()
            .AddJsonFile(_tempFile, false, true)
            .Build();

        var oldComponent = new SiteComponentConfig {
            Name = "proxy",
            Config = config.GetSection("S"),
        };

        Thread.Sleep(100);
        File.WriteAllText(_tempFile, """{"S":{"Target":"http://backend:6000"}}""");
        Thread.Sleep(500);

        var newComponent = new SiteComponentConfig {
            Name = "proxy",
            Config = config.GetSection("S"),
        };

        Assert.That(oldComponent, Is.Not.EqualTo(newComponent));
    }

    [Test]
    public void Equals_returns_true_after_reload_when_data_unchanged()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, """{"S":{"Target":"http://backend:5000"}}""");

        var config = new ConfigurationBuilder()
            .AddJsonFile(_tempFile, false, true)
            .Build();

        var oldComponent = new SiteComponentConfig {
            Name = "proxy",
            Config = config.GetSection("S"),
        };

        Thread.Sleep(100);
        File.WriteAllText(_tempFile, """{"Other":{"Foo":"bar"},"S":{"Target":"http://backend:5000"}}""");
        Thread.Sleep(500);

        var newComponent = new SiteComponentConfig {
            Name = "proxy",
            Config = config.GetSection("S"),
        };

        Assert.That(oldComponent, Is.EqualTo(newComponent));
    }
}
