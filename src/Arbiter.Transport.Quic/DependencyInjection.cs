using System.Net.Quic;
using Arbiter.Application;
using Arbiter.Application.Configuration;
using Arbiter.Application.Middleware;
using Arbiter.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Transport.Quic;

public static class DependencyInjection
{
    public static void AddQuicTransport(this IServiceCollection services)
    {
        if (!QuicListener.IsSupported)
            return;

        services.AddTransport<QuicAcceptor, QuicTransportConfig>("quic");
        services.AddGlobalMiddleware<AltSvcGlobalMiddleware>();
    }
}
