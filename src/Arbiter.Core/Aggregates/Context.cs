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
        get; private set;
    }

    public async Task<Stream> AcceptUpgradeAsync(ReadOnlyHeaders? responseHeaders = null)
    {
        if (Request.Upgrade is null)
            throw new InvalidOperationException("Request is not an upgrade request");

        var stream = await Request.Upgrade.AcceptAsync(responseHeaders);
        IsUpgraded = true;

        return stream;
    }
}
