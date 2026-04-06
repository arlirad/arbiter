using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;

namespace Arbiter.Core.Factories;

public class ContextFactory : IContextFactory
{
    public Context? Create(
        Method method,
        string path,
        IEnumerable<KeyValuePair<string, List<string>>> headers,
        Stream? stream,
        bool isWebSocketUpgrade)
    {
        var request = RequestContextFactory.Create(method, path, headers, stream, isWebSocketUpgrade);
        var response = ResponseContextFactory.Create();

        if (request is null || response is null)
            return null;

        return new Context(request, response);
    }
}