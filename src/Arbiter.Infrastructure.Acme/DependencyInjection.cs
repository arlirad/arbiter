using Arbiter.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Infrastructure.Acme;

public static class DependencyInjection
{
    public static void AddAcmeInfrastructure(this IServiceCollection services)
    {
        services.AddKeyedScoped<IMiddleware, AcmeMiddleware>("acme");
        services.AddKeyedScoped<IWorker, AcmeWorker>("acme");
    }
}