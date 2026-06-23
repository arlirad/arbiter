using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Arbiter.Application.Tests;

public class TestConfiguration : IConfiguration
{
    private readonly ConcurrentDictionary<string, string?> _data = new();
    private readonly TestReloadToken _rootToken = new();

    public TestConfiguration(Dictionary<string, string?> initial)
    {
        foreach (var kvp in initial)
            _data[kvp.Key] = kvp.Value;
    }

    public string? this[string key]
    {
        get => _data.TryGetValue(key, out var v) ? v ?? "" : "";
        set => _data[key] = value ?? "";
    }

    public IConfigurationSection GetSection(string key) => new TestSection(this, key);

    public IEnumerable<IConfigurationSection> GetChildren() => [];

    public IChangeToken GetReloadToken() => _rootToken;

    public void SetValue(string key, string? value) => _data[key] = value;

    public void FireRootReload() => _rootToken.Fire();

    private class TestSection(TestConfiguration root, string key) : IConfigurationSection
    {
        public string? Value
        {
            get => root._data.GetValueOrDefault(key);
            set => root._data[key] = value;
        }

        public string Key => key.Contains(':') ? key.Split(':').Last() : key;

        public string Path => key;

        public string? this[string key1]
        {
            get => root[key + ":" + key1];
            set => root[key + ":" + key1] = value ?? "";
        }

        public IConfigurationSection GetSection(string key1) => new TestSection(root, key + ":" + key1);

        public IEnumerable<IConfigurationSection> GetChildren() => [];

        public IChangeToken GetReloadToken() => root._rootToken;
    }
}

public class TestReloadToken : IChangeToken
{
    private readonly CancellationTokenSource _cts = new();

    public bool ActiveChangeCallbacks => true;

    public bool HasChanged => _cts.IsCancellationRequested;

    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => _cts.Token.Register(callback, state);

    public CancellationToken WaitForChange(CancellationToken cancellationToken)
    {
        return _cts.Token.WaitHandle.WaitOne()
            ? CancellationToken.None
            : cancellationToken;
    }

    public void Fire()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // already fired (first fire after construction)
        }
    }
}
