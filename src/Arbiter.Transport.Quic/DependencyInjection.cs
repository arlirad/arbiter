using Arbiter.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Transport.Quic;

public static class DependencyInjection
{
    public static void AddQuicTransport(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurator, QuicAcceptorConfigurator>();
        services.AddSingleton<IAcceptor, QuicAcceptor>();
    }
}