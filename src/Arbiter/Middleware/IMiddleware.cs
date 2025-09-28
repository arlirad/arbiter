using Arbiter.Models;
using Arbiter.Models.Network;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Middleware;

internal delegate Task HandleDelegate(HttpContext context);

internal interface IMiddleware
{
    public Task Configure(Site site, IConfiguration config);
    public Task Handle(HttpContext context);
}