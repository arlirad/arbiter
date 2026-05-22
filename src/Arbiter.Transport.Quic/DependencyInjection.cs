using System.Net.Quic;
using Arbiter.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Transport.Quic;

public static class DependencyInjection
{
    public static void AddQuicTransport(this IServiceCollection services)
    {
        if (!QuicListener.IsSupported)
            return;

        services.AddKeyedSingleton<IAcceptor, QuicAcceptor>("quic");
    }
}
