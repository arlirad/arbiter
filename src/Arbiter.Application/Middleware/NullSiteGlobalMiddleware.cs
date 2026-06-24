using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;

namespace Arbiter.Application.Middleware;

public class NullSiteGlobalMiddleware(GlobalHandleDelegate next) : IGlobalMiddleware
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
