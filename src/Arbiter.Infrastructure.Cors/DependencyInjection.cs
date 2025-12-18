using Arbiter.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Infrastructure.Cors;

public static class DependencyInjection
{
    public static void AddCorsInfrastructure(this IServiceCollection services)
    {
        services.AddKeyedScoped<IMiddleware, CorsMiddleware>("cors");
    }
}