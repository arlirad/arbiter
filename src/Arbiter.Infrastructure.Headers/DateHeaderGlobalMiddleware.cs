using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;

namespace Arbiter.Infrastructure.Headers;

public class DateHeaderGlobalMiddleware(HandleDelegate next) : IGlobalMiddleware
{
    public async Task Handle(ITransaction transaction, Site? site, Context context)
    {
        context.Response.AddHeader("Date", DateTimeOffset.UtcNow.ToString("R"));
        await next(transaction, site, context);
    }
}
