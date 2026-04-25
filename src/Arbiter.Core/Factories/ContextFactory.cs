using System.Net;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;

namespace Arbiter.Core.Factories;

public class ContextFactory : IContextFactory
{
    public Context? Create(
        int transactionId,
        Method method,
        string path,
        IEnumerable<KeyValuePair<string, List<string>>> headers,
        Stream? stream,
        bool isWebSocketUpgrade,
        string? authority,
        bool isSecure,
        IPAddress? remoteAddress)
    {
        var request = RequestContextFactory.Create(transactionId, method, path, headers, stream, isWebSocketUpgrade,
            authority, isSecure, remoteAddress);

        var response = ResponseContextFactory.Create();

        return request is null || response is null ? null : new Context(request, response);
    }
}