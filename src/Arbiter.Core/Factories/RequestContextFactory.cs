using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Core.Factories;

public class RequestContextFactory
{
    public static RequestContext? Create(
        Method method,
        string path,
        IEnumerable<KeyValuePair<string, List<string>>> headers,
        Stream? stream,
        bool isWebSocketUpgrade,
        string? authority,
        bool isSecure,
        string? remoteAddress)
    {
        var headersConcrete = new Headers();

        foreach (var header in headers)
        {
            headersConcrete[header.Key] = header.Value;
        }

        return new RequestContext(method, path, headersConcrete, stream, isWebSocketUpgrade, authority, isSecure, remoteAddress);
    }
}