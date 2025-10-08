using Arbiter.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Transport.Tcp;

public static class DependencyInjection
{
    public static void AddTcpTransport(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurator, TcpAcceptorConfigurator>();
        services.AddSingleton<IAcceptor, TcpAcceptor>();
    }
}