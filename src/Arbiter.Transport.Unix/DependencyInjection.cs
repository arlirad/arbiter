using Arbiter.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Transport.Unix;

public static class DependencyInjection
{
    public static void AddUnixSocketTransport(this IServiceCollection services) => services.AddKeyedSingleton<IAcceptor, UnixSocketAcceptor>("unix");
}
