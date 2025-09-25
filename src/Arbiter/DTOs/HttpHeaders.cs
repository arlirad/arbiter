using System.Collections;

namespace Arbiter.DTOs;

public class HttpHeaders
{
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

    public string? this[string name]
    {
        get => Get(name);
        set => Set(name, value);
    }

    public Dictionary<string, string>.Enumerator GetEnumerator()
    {
        return _headers.GetEnumerator();
    }

    private string? Get(string name)
    {
        return _headers.TryGetValue(name, out var value) ? value : null;
    }

    private void Set(string name, string? value)
    {
        if (value is null)
        {
            _headers.Remove(name);
            return;
        }

        _headers[name] = value;
    }
}