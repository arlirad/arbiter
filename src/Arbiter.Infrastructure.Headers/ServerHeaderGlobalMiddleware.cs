using System.Reflection;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Constants;

namespace Arbiter.Infrastructure.Headers;

public class ServerHeaderGlobalMiddleware(HandleDelegate next) : IGlobalMiddleware
{
    private static readonly string ServerHeader = $"{AppConstants.Name}/{typeof(ServerHeaderGlobalMiddleware).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]}";

    public async Task Handle(ITransaction transaction, Site? site, Context context)
    {
        context.Response.AddHeader("Server", ServerHeader);
        await next(transaction, site, context);
    }
}
