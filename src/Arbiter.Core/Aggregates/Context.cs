using Arbiter.Core.ValueObjects;

namespace Arbiter.Core.Aggregates;

public class Context
{
    internal Context(RequestContext request, ResponseContext response)
    {
        Request = request;
        Response = response;
    }

    public RequestContext Request
    {
        get;
    }
    public ResponseContext Response
    {
        get;
    }
    public bool IsUpgraded
    {
        get;
        set;
    }
}
