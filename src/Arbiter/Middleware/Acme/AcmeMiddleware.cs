using Arbiter.Models;
using Arbiter.Models.Network;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Middleware.Acme;

internal class AcmeMiddleware(HandleDelegate next) : IMiddleware
{
    public Task Configure(Site site, IConfiguration config)
    {
        return Task.CompletedTask;
    }

    public async Task Handle(HttpContext context)
    {
        Log.Information("acme: Before next");
        await next(context);
        Log.Information("acme: After next");
    }
}