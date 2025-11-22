using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Enums;
using Arbiter.Domain.Interfaces;

namespace Arbiter.Domain.Factories;

public class ContextFactory : IContextFactory
{
    public Context? Create(
        Method method,
        string path,
        IEnumerable<KeyValuePair<string, string>> headers,
        Stream? stream)
    {
        var request = RequestContextFactory.Create(method, path, headers, stream);
        var response = ResponseContextFactory.Create();

        if (request is null || response is null)
            return null;

        return new Context(request, response);
    }
}