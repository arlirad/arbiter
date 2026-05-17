using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Constants;

namespace Arbiter.Application.Middleware;

public class ServerHeaderGlobalMiddleware(HandleDelegate next) : IGlobalMiddleware
{
    private static readonly string ServerHeader = $"{AppConstants.Name}/{typeof(ServerHeaderGlobalMiddleware).Assembly.GetName().Version?.ToString(fieldCount: 3)}";

    public async Task Handle(ITransaction transaction, Site? site, Context context)
    {
        context.Response.Headers.Add("Server", ServerHeader);
        await next(transaction, site, context);
    }
}