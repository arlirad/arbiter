using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Arbiter.Application.Configuration;

public class ConfigurationScope : IConfiguration
{
    private readonly string[] _paths;
    private readonly ConfigurationReloadToken _reloadToken = new();
    private readonly IConfiguration _root;
    private readonly IConfiguration _scoped;
    private string _snapshot = "";

    public ConfigurationScope(IConfiguration root, params string[] paths)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _paths = paths ?? [];

        _scoped = _paths.Length == 1
            ? root.GetSection(_paths[0])
            : root;

        UpdateSnapshot();
        ChangeToken.OnChange(root.GetReloadToken, OnRootChanged);
    }

    public string this[string key]
    {
        get => _scoped[key]!;
        set => _scoped[key] = value!;
    }

    public IEnumerable<IConfigurationSection> GetChildren() => _scoped.GetChildren();

    public IChangeToken GetReloadToken() => _reloadToken;

    public IConfigurationSection GetSection(string key) => _scoped.GetSection(key);

    private void OnRootChanged()
    {
        var previous = _snapshot;
        UpdateSnapshot();

        if (previous != _snapshot)
            _reloadToken.OnReload();
    }

    private void UpdateSnapshot()
    {
        var lines = new List<string>();

        foreach (var path in _paths)
        {
            var section = _root.GetSection(path);
            FlattenSection(section, lines);
        }

        _snapshot = string.Join("\n", lines);
    }

    private static void FlattenSection(IConfigurationSection section, List<string> lines)
    {
        var value = section.Value;
        if (value is not null)
            lines.Add($"{section.Path}={value}");

        foreach (var child in section.GetChildren())
            FlattenSection(child, lines);
    }
}