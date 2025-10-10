using Arbiter.Application.Interfaces;
using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Enums;

namespace Arbiter.Application.Middleware;

public class NullSiteGlobalMiddleware(HandleDelegate next) : IGlobalMiddleware
{
    public async Task Handle(ITransaction transaction, Site? site, Context context)
    {
        if (site is null)
        {
            await context.Response.Set(Status.NotFound);
            return;
        }

        await next(transaction, site, context);
    }
}