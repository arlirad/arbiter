using System.Net.Quic;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Middleware;
using Arbiter.Transport.Quic.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Transport.Quic;

public static class DependencyInjection
{
    public static void AddQuicTransport(this IServiceCollection services)
    {
        if (!QuicListener.IsSupported)
            return;

        services.AddSingleton<IConfigurator, QuicAcceptorConfigurator>();
        services.AddSingleton<IConfigurator, QuicAltSvcGlobalMiddlewareConfigurator>();
        services.AddSingleton<IAcceptor, QuicAcceptor>();
    }

    public static void AddQuicGlobalMiddleware(this IServiceCollection services)
    {
        services.AddGlobalMiddleware<QuicAltSvcGlobalMiddleware>();
    }
}