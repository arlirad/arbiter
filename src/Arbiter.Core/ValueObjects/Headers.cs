using System.Collections;

namespace Arbiter.Core.ValueObjects;

public class Headers : IEnumerable<KeyValuePair<string, List<string>>>
{
    private readonly Dictionary<string, List<string>> _headers = new(StringComparer.OrdinalIgnoreCase);

    public List<string>? this[string name]
    {
        get => Get(name);
        set => Set(name, value);
    }

    public string? AltSvc
    {
        get => Get("alt-svc")?.FirstOrDefault() ?? null;
        set => Set("alt-svc", value is not null ? [value] : null);
    }
    public string? ContentType
    {
        get => Get("content-type")?.FirstOrDefault() ?? null;
        set => Set("content-type", value is not null ? [value] : null);
    }
    public string? ContentLength
    {
        get => Get("content-length")?.FirstOrDefault() ?? null;
        set => Set("content-length", value is not null ? [value] : null);
    }
    public string? Host
    {
        get => Get("host")?.FirstOrDefault() ?? null;
        set => Set("host", value is not null ? [value] : null);
    }
    public string? TransferEncoding
    {
        get => Get("transfer-encoding")?.FirstOrDefault() ?? null;
        set => Set("transfer-encoding", value is not null ? [value] : null);
    }

    public IEnumerator<KeyValuePair<string, List<string>>> GetEnumerator() => _headers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private List<string>? Get(string name) => _headers.GetValueOrDefault(name);

    private void Set(string name, List<string>? value)
    {
        if (value is null)
        {
            _headers.Remove(name);
            return;
        }

        _headers[Canonicalize(name)] = value;
    }

    public void Add(string headerKey, string headerValue)
    {
        var key = Canonicalize(headerKey);
        if (_headers.TryGetValue(key, out var list))
            list.Add(headerValue);
        else
            _headers[key] = [headerValue];
    }

    public void Replace(string headerKey, string headerValue) => _headers[Canonicalize(headerKey)] = [headerValue];

    private static string Canonicalize(string name)
    {
        var span = name.AsSpan();
        Span<char> result = stackalloc char[span.Length];
        var nextUpper = true;

        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            result[i] = nextUpper ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
            nextUpper = c == '-';
        }

        return new string(result);
    }
}