using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Configuration;

public class SiteComponentConfig : IEquatable<SiteComponentConfig?>
{
    public string? Name
    {
        get;
        init;
    }

    private readonly IConfiguration? _frozenConfig;

    public IConfigurationSection? Config
    {
        get;
        init {
            field = value;
            _frozenConfig = value is null ? null : Freeze(value);
        }
    }

    public bool Equals(SiteComponentConfig? other) => other is not null && (ReferenceEquals(this, other) || (Name == other.Name && ConfigEquals(_frozenConfig, other._frozenConfig)));
    public override bool Equals(object? obj) => Equals(obj as SiteComponentConfig);
    public override int GetHashCode() => HashCode.Combine(Name, ConfigGetHashCode(_frozenConfig));

    private static IConfiguration Freeze(IConfiguration config)
    {
        var dict = config.AsEnumerable()
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict!)
            .Build();
    }

    private static bool ConfigEquals(IConfiguration? a, IConfiguration? b) => (a is null && b is null) || (a is not null && b is not null && ConfigEqualsContent(a, b));

    private static bool ConfigEqualsContent(IConfiguration a, IConfiguration b)
    {
        var ae = a.AsEnumerable().OrderBy(kv => kv.Key).ToList();
        var be = b.AsEnumerable().OrderBy(kv => kv.Key).ToList();

        return ae.Count == be.Count && ae.Zip(be).All(pair =>
            pair.First.Key == pair.Second.Key && pair.First.Value == pair.Second.Value);
    }

    private static int ConfigGetHashCode(IConfiguration? config)
    {
        return config is not null
            ? config.AsEnumerable()
                .OrderBy(kv => kv.Key)
                .Aggregate(0, (hash, kvp) => HashCode.Combine(hash, kvp.Key, kvp.Value))
            : 0;
    }
}