using Arbiter.Application.Interfaces;
using Arbiter.Application.Services;
using Arbiter.Core.Aggregates;

namespace Arbiter.Infrastructure.Headers;

public class AltSvcGlobalMiddleware(HandleDelegate next, AltSvcService altSvc) : IGlobalMiddleware
{
    public Task Handle(ITransaction transaction, Site? site, Context context)
    {
        if (altSvc.HeaderValue is { } value)
            context.Response.SetHeader("Alt-Svc", value);

        return next(transaction, site, context);
    }
}
