using Arbiter.Application.Interfaces;
using Arbiter.Application.Services;
using Arbiter.Infrastructure.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Protocol.Http11;

public static class DependencyInjection
{
    public static IServiceCollection AddHttp11Protocol(this IServiceCollection services)
    {
        services.AddSingleton<IProtocolRegistration>(sp => {
            var tip = sp.GetRequiredService<TransactionIdProvider>();

            return new ProtocolRegistration(Core.Enums.Protocol.Http11, _ => new Http11Protocol(tip));
        });

        return services;
    }
}
