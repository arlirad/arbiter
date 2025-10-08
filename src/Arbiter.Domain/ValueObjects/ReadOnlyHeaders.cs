using System.Collections;

namespace Arbiter.Domain.ValueObjects;

public class ReadOnlyHeaders(Headers headers) : IEnumerable<KeyValuePair<string, string>>
{
    public string? this[string name]
    {
        get => Get(name);
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => headers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private string? Get(string name) => headers[name];
}