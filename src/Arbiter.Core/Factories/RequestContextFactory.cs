using System.Net;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Core.Factories;

public class RequestContextFactory
{
    public static RequestContext? Create(
        int transactionId,
        Method method,
        string path,
        IEnumerable<KeyValuePair<string, List<string>>> headers,
        Stream? stream,
        IUpgrade? upgrade,
        string? authority,
        bool isSecure,
        IPAddress? remoteAddress)
    {
        var headersConcrete = new Headers();

        foreach (var header in headers)
        {
            headersConcrete[header.Key] = header.Value;
        }

        return new RequestContext(transactionId, method, path, headersConcrete, stream,
            upgrade, authority, isSecure, remoteAddress);
    }
}
