using Arbiter.Domain.Enums;
using Arbiter.Domain.ValueObjects;

namespace Arbiter.Domain.Factories;

public class RequestContextFactory
{
    public static RequestContext? Create(
        Method method,
        string path,
        IEnumerable<KeyValuePair<string, string>> headers,
        Stream? stream)
    {
        var headersConcrete = new Headers();

        foreach (var header in headers)
        {
            headersConcrete[header.Key] = header.Value;
        }

        return new RequestContext(method, path, headersConcrete, stream);
    }
}