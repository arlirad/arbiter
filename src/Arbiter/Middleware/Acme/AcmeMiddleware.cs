using Arbiter.Models;
using Arbiter.Models.Network;

namespace Arbiter.Middleware.Acme;

internal class AcmeMiddleware : IMiddleware
{
    public string Name => "acme";

    public Task Configure(Site site, object config)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CanHandle(HttpRequestContext request)
    {
        throw new NotImplementedException();
    }

    public Task Handle(HttpRequestContext request, HttpResponseContext response)
    {
        throw new NotImplementedException();
    }
}