using Arbiter.Models;
using Arbiter.Models.Network;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Middleware.CSharp;

internal class CSharpMiddleware(HandleDelegate next) : IMiddleware
{
    public Task Configure(Site site, IConfiguration config)
    {
        return Task.CompletedTask;
    }

    public Task Handle(HttpContext context)
    {
        throw new NotImplementedException();
    }
}