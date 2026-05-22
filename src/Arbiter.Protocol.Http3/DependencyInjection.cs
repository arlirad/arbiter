using System.Net.Quic;
using System.Runtime.Versioning;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Services;
using Arbiter.Infrastructure.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Protocol.Http3;

public static class DependencyInjection
{
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macOS")]
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddHttp3Protocol(this IServiceCollection services)
    {
        if (!QuicListener.IsSupported)
            return services;

        services.AddSingleton<IProtocolRegistration>(sp => {
            var tip = sp.GetRequiredService<TransactionIdProvider>();

            return new ProtocolRegistration(Core.Enums.Protocol.Http3, _ => new Http3Protocol(tip));
        });

        return services;
    }
}
