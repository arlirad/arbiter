using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;

namespace Arbiter.Infrastructure.Headers;

public class RequestIdGlobalMiddleware(HandleDelegate next) : IGlobalMiddleware
{
    public async Task Handle(ITransaction transaction, Site? site, Context context)
    {
        context.Response.AddHeader("X-Request-Id", transaction.Id.ToString());
        await next(transaction, site, context);
    }
}
