using Microsoft.Extensions.Configuration;

namespace Arbiter.Configuration.Tests;

[TestFixture]
public class ConfigurationProviderTests
{
    [TearDown]
    public void TearDown()
    {
        if (_tempFile != null && File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    private string? _tempFile;

    private record TestConfig(string? Value);

    [Test]
    public void Observe_emits_initial_value()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Test:Value"] = "hello",
            })
            .Build();

        using var provider = new ConfigurationProvider(config);
        var values = new List<TestConfig>();

        provider.Observe<TestConfig>("Test").Subscribe(values.Add);

        Assert.That(values, Has.Count.EqualTo(1));
        Assert.That(values[0].Value, Is.EqualTo("hello"));
    }

    [Test]
    public void Observe_caches_observable_per_section()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Test:Value"] = "hello",
            })
            .Build();

        using var provider = new ConfigurationProvider(config);
        var observable1 = provider.Observe<TestConfig>("Test");
        var observable2 = provider.Observe<TestConfig>("Test");

        var values1 = new List<TestConfig>();
        var values2 = new List<TestConfig>();

        observable1.Subscribe(values1.Add);
        observable2.Subscribe(values2.Add);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values1, Has.Count.EqualTo(1));
            Assert.That(values2, Has.Count.EqualTo(1));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values1[0].Value, Is.EqualTo("hello"));
            Assert.That(values2[0].Value, Is.EqualTo("hello"));
        }
    }

    [Test]
    public void Observe_emits_on_section_change()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, """{"Test":{"Value":"hello"}}""");

        var config = new ConfigurationBuilder()
            .AddJsonFile(_tempFile, false, true)
            .Build();

        using var provider = new ConfigurationProvider(config);
        var values = new List<TestConfig>();

        provider.Observe<TestConfig>("Test").Subscribe(values.Add);

        Assert.That(values, Has.Count.EqualTo(1));
        Assert.That(values[0].Value, Is.EqualTo("hello"));

        Thread.Sleep(100);
        File.WriteAllText(_tempFile, """{"Test":{"Value":"world"}}""");

        Thread.Sleep(500);

        Assert.That(values, Has.Count.EqualTo(2));
        Assert.That(values[1].Value, Is.EqualTo("world"));
    }

    [Test]
    public void Observe_does_not_emit_when_other_section_changes()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, """{"Test":{"Value":"hello"},"Other":{"Value":"unchanged"}}""");

        var config = new ConfigurationBuilder()
            .AddJsonFile(_tempFile, false, true)
            .Build();

        using var provider = new ConfigurationProvider(config);
        var values = new List<TestConfig>();

        provider.Observe<TestConfig>("Test").Subscribe(values.Add);

        Assert.That(values, Has.Count.EqualTo(1));

        Thread.Sleep(50);
        File.WriteAllText(_tempFile, """{"Test":{"Value":"hello"},"Other":{"Value":"changed"}}""");

        Thread.Sleep(100);

        Assert.That(values, Has.Count.EqualTo(1));
    }

    [Test]
    public void Dispose_completes_observables()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Test:Value"] = "hello",
            })
            .Build();

        var provider = new ConfigurationProvider(config);
        var completed = false;
        var values = new List<TestConfig>();

        provider.Observe<TestConfig>("Test").Subscribe(
            values.Add,
            () => completed = true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values, Has.Count.EqualTo(1));
            Assert.That(completed, Is.False);
        }

        provider.Dispose();

        Assert.That(completed, Is.True);
    }

    [Test]
    public void Observe_skips_bad_values()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Test"] = "not-an-object",
            })
            .Build();

        using var provider = new ConfigurationProvider(config);
        var values = new List<TestConfig>();

        provider.Observe<TestConfig>("Test").Subscribe(values.Add);

        Assert.That(values, Is.Empty);
    }
}
