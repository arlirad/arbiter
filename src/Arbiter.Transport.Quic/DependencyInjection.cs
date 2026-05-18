using System.Net.Quic;
using Arbiter.Application;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Transport.Quic;

public static class DependencyInjection
{
    public static void AddQuicTransport(this IServiceCollection services)
    {
        if (!QuicListener.IsSupported)
            return;

        services.AddSingleton<QuicPortService>();
        services.AddTransport<QuicAcceptor, QuicTransportConfig>("quic");
    }

    public static void AddQuicGlobalMiddleware(this IServiceCollection services)
    {
        if (QuicListener.IsSupported)
            services.AddGlobalMiddleware<QuicAltSvcGlobalMiddleware>();
    }
}