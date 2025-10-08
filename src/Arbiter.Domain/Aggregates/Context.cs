using Arbiter.Domain.ValueObjects;

namespace Arbiter.Domain.Aggregates;

public class Context
{
    internal Context(RequestContext request, ResponseContext response)
    {
        Request = request;
        Response = response;
    }

    public RequestContext Request { get; }
    public ResponseContext Response { get; }
}