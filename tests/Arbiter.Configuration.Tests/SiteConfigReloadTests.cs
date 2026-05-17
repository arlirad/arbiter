using Arbiter.Application.Configuration;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Configuration.Tests;

[TestFixture]
public class SiteConfigReloadTests
{
    private string? _tempFile;

    [TearDown]
    public void TearDown()
    {
        if (_tempFile != null && File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Test]
    public void Observe_SiteConfig_emits_on_file_change()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, """{"Sites":{"test":{"Path":"/var/www","Bindings":["http://localhost:8080"],"Middleware":[{"Name":"proxy","Config":{"Target":"http://backend:5000"}}]}}}""");

        var config = new ConfigurationBuilder()
            .AddJsonFile(_tempFile, optional: false, reloadOnChange: true)
            .Build();

        using var provider = new Arbiter.Configuration.ConfigurationProvider(config);
        var values = new List<Dictionary<string, SiteConfig>>();

        provider.Observe<Dictionary<string, SiteConfig>>("Sites").Subscribe(values.Add);

        Assert.That(values, Has.Count.EqualTo(1));
        Assert.That(values[0]["test"].Path, Is.EqualTo("/var/www"));

        Thread.Sleep(100);
        File.WriteAllText(_tempFile, """{"Sites":{"test":{"Path":"/var/www","Bindings":["http://localhost:8080"],"Middleware":[{"Name":"proxy","Config":{"Target":"http://backend:6000"}}]}}}""");

        Thread.Sleep(500);

        Assert.That(values, Has.Count.EqualTo(2));
        var middleware = values[1]["test"].Middleware;
        Assert.That(middleware, Is.Not.Null);
        Assert.That(middleware!.Count, Is.EqualTo(1));
    }
}
