namespace Arbiter.Application.Configuration;

public class SiteConfig : IEquatable<SiteConfig?>
{
    public string? Path
    {
        get;
    }
    public List<Uri>? Bindings
    {
        get;
        set;
    }
    public List<string>? DefaultFiles
    {
        get;
        set;
    }
    public List<string>? Handlers
    {
        get;
        set;
    }

    public List<SiteComponentConfig>? Middleware
    {
        get;
        set;
    }
    public List<SiteComponentConfig>? Workers
    {
        get;
        set;
    }

    public bool Equals(SiteConfig? other)
    {
        return other is not null
            && (ReferenceEquals(this, other)
                || (Path == other.Path
                    && SequenceEqual(Bindings, other.Bindings)
                    && SequenceEqual(DefaultFiles, other.DefaultFiles)
                    && SequenceEqual(Handlers, other.Handlers)
                    && ListEquals(Middleware, other.Middleware)
                    && ListEquals(Workers, other.Workers)));
    }

    public override bool Equals(object? obj) => Equals(obj as SiteConfig);
    public override int GetHashCode() => Path?.GetHashCode() ?? 0;

    private static bool SequenceEqual<T>(List<T>? a, List<T>? b)
    {
        return a is not null && b is not null
            ? a.Count == b.Count && a.Zip(b).All(pair => EqualityComparer<T>.Default.Equals(pair.First, pair.Second))
            : a is null && b is null;
    }

    private static bool ListEquals(List<SiteComponentConfig>? a, List<SiteComponentConfig>? b)
    {
        return a is not null && b is not null
            ? a.Count == b.Count && a.Zip(b).All(pair => pair.First.Equals(pair.Second))
            : a is null && b is null;
    }
}