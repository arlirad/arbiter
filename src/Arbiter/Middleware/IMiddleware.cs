using Arbiter.Models;
using Arbiter.Models.Network;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Middleware;

internal interface IMiddleware
{
    public Task Configure(Site site, IConfigurationSection config);

    public Task<bool> CanHandle(HttpRequestContext request);
    public Task Handle(HttpRequestContext request, HttpResponseContext response);
}