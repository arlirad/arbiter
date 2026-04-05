using Arbiter.Domain.Enums;

namespace Arbiter.Domain.ValueObjects;

public class RequestContext
{
    internal RequestContext(Method method, string path, Headers headers, Stream? stream, bool isWebSocketUpgrade)
    {
        Method = method;
        Path = path;
        Headers = new ReadOnlyHeaders(headers);
        Stream = stream;
        IsWebSocketUpgrade = isWebSocketUpgrade;
    }

    public Method Method { get; }
    public string Path { get; set; }
    public ReadOnlyHeaders Headers { get; }
    public Stream? Stream { get; private set; }
    public bool IsWebSocketUpgrade { get; }
}