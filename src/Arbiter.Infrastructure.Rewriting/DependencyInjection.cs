using Arbiter.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Infrastructure.Rewriting;

public static class DependencyInjection
{
    public static void AddRewritingInfrastructure(this IServiceCollection services)
    {
        services.AddKeyedScoped<IMiddleware, RewritingMiddleware>("rewrite");
    }
}