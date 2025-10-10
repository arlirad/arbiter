using Arbiter.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Transport.Quic.Configuration;

internal class QuicAltSvcGlobalMiddlewareConfigurator(QuicAltSvcGlobalMiddleware middleware) : IConfigurator
{
    public Task Configure(IConfiguration config)
    {
        var quicPorts = config.GetSection("QuicPorts").Get<List<int>>();

        if (quicPorts is null)
            return Task.CompletedTask;

        middleware.SetPorts(quicPorts);

        return Task.CompletedTask;
    }
}