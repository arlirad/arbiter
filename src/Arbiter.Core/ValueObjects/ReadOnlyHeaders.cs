using System.Collections;

namespace Arbiter.Core.ValueObjects;

public class ReadOnlyHeaders(Headers headers) : IEnumerable<KeyValuePair<string, List<string>>>
{
    public IReadOnlyList<string>? this[string name] => Get(name);

    public string? AltSvc => Get("alt-svc")?.FirstOrDefault() ?? null;
    public string? ContentType => Get("content-type")?.FirstOrDefault() ?? null;
    public string? ContentLength => Get("content-length")?.FirstOrDefault() ?? null;
    public string? Host => Get("host")?.FirstOrDefault() ?? null;

    public IEnumerator<KeyValuePair<string, List<string>>> GetEnumerator() => headers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IReadOnlyList<string>? Get(string name) => headers[name];
}
