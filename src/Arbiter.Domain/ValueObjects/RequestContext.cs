using Arbiter.Domain.Enums;

namespace Arbiter.Domain.ValueObjects;

public class RequestContext
{
    internal RequestContext(Method method, string path, Headers headers)
    {
        Method = method;
        Path = path;
        Headers = new ReadOnlyHeaders(headers);
    }

    public Method Method { get; }
    public string Path { get; }
    public ReadOnlyHeaders Headers { get; }
}