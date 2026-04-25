using Arbiter.Application.Configuration;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Tests;

[TestFixture]
public class ConfigurationScopeTests
{
    [Test]
    public void Ctor_InitializesSnapshot()
    {
        var config = new TestConfiguration(new Dictionary<string, string?> {
            ["Sites:default:Host"] = "example.com",
            ["Sites:default:Port"] = "443",
        });

        var scope = new ConfigurationScope(config, "Sites");

        var section = scope.GetSection("Sites:default");
        Assert.That(section["Host"], Is.EqualTo("example.com"));
        Assert.That(section["Port"], Is.EqualTo("443"));
    }

    [Test]
    public void GetSection_ForwardsToRoot()
    {
        var config = new TestConfiguration(new Dictionary<string, string?> {
            ["Sites:default:Host"] = "example.com",
            ["Sites:default:Port"] = "443",
        });

        var scope = new ConfigurationScope(config, "Sites");

        var section = scope.GetSection("Sites:default");

        Assert.That(section["Host"], Is.EqualTo("example.com"));
        Assert.That(section["Port"], Is.EqualTo("443"));
    }

    [Test]
    public void NoChange_NoFireOnConstruction()
    {
        var config = new TestConfiguration(new Dictionary<string, string?> {
            ["Sites:default:Host"] = "example.com",
        });

        var fireCount = 0;
        var scope = new ConfigurationScope(config, "Sites");
    }
}