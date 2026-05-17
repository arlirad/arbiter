using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Configuration;

public class SiteComponentConfig : IEquatable<SiteComponentConfig?>
{
    public string? Name
    {
        get;
        init;
    }
    public IConfigurationSection? Config
    {
        get;
        init;
    }

    public bool Equals(SiteComponentConfig? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (Name != other.Name)
            return false;

        var a = Config?.AsEnumerable().OrderBy(kv => kv.Key).ToList() ?? [];
        var b = other.Config?.AsEnumerable().OrderBy(kv => kv.Key).ToList() ?? [];

        return a.Count == b.Count && a.Zip(b).All(pair =>
            pair.First.Key == pair.Second.Key && pair.First.Value == pair.Second.Value);
    }

    public override bool Equals(object? obj) => Equals(obj as SiteComponentConfig);
    public override int GetHashCode() => Name?.GetHashCode() ?? 0;
}