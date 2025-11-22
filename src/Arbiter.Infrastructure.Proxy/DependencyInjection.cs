using Arbiter.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Infrastructure.Proxy;

public static class DependencyInjection
{
    public static void AddProxyInfrastructure(this IServiceCollection services)
    {
        services.AddKeyedScoped<IMiddleware, ProxyMiddleware>("proxy");
    }
}