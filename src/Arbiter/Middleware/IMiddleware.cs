using Arbiter.Models;
using Arbiter.Models.Network;

namespace Arbiter.Middleware;

internal interface IMiddleware
{
    public Task Configure(Site site, object config);

    public Task<bool> CanHandle(HttpRequestContext request);
    public Task Handle(HttpRequestContext request, HttpResponseContext response);
}