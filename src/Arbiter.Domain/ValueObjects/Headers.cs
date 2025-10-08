using System.Collections;

namespace Arbiter.Domain.ValueObjects;

public class Headers : IEnumerable<KeyValuePair<string, string>>
{
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

    public string? this[string name]
    {
        get => Get(name);
        set => Set(name, value);
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return _headers.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private string? Get(string name)
    {
        return _headers.GetValueOrDefault(name);
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