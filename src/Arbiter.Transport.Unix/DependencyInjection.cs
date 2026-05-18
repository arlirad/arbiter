using Arbiter.Application;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Transport.Unix;

public static class DependencyInjection
{
    public static void AddUnixSocketTransport(this IServiceCollection services) => services.AddTransport<UnixSocketAcceptor, UnixTransportConfig>("unix");
}