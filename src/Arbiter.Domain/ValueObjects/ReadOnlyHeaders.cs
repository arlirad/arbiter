using System.Collections;

namespace Arbiter.Domain.ValueObjects;

public class ReadOnlyHeaders(Headers headers) : IEnumerable<KeyValuePair<string, List<string>>>
{
    public List<string>? this[string name]
    {
        get => Get(name);
    }

    public string? AltSvc { get => Get("alt-svc")?.FirstOrDefault() ?? null; }
    public string? ContentType { get => Get("content-type")?.FirstOrDefault() ?? null; }
    public string? ContentLength { get => Get("content-length")?.FirstOrDefault() ?? null; }
    public string? Host { get => Get("host")?.FirstOrDefault() ?? null; }

    public IEnumerator<KeyValuePair<string, List<string>>> GetEnumerator() => headers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private List<string>? Get(string name) => headers[name];
}