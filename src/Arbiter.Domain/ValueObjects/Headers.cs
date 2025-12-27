using System.Collections;

namespace Arbiter.Domain.ValueObjects;

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

    public IEnumerator<KeyValuePair<string, List<string>>> GetEnumerator()
    {
        return _headers.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private List<string>? Get(string name)
    {
        return _headers.GetValueOrDefault(name);
    }

    private void Set(string name, List<string>? value)
    {
        if (value is null)
        {
            _headers.Remove(name);
            return;
        }

        _headers[name] = value;
    }

    public void Add(string headerKey, string headerValue)
    {
        _headers[headerKey] = _headers.GetValueOrDefault(headerKey) ?? [headerValue];
    }

    public void Replace(string headerKey, string headerValue)
    {
        _headers[headerKey] = [headerValue];
    }
}