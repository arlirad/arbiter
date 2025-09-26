using Arbiter.Models;
using Arbiter.Models.Network;

namespace Arbiter.Middleware.CSharp;

internal class CSharpMiddleware : IMiddleware
{
    public Task Configure(Site site, object config)
    {
        return Task.CompletedTask;
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