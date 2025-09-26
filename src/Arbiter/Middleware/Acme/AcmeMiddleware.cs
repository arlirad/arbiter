using Arbiter.Models;
using Arbiter.Models.Network;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Middleware.Acme;

internal class AcmeMiddleware : IMiddleware
{
    public Task Configure(Site site, IConfigurationSection config)
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