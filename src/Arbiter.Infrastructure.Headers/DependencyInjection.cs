using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Middleware;

namespace Arbiter.Infrastructure.Headers;

public static class DependencyInjection
{
    public static void ConfigureHeaderMiddleware(this GlobalMiddlewareChain chain, ServerHeadersConfig headersConfig)
    {
        if (headersConfig.Server)
            chain.Add(next => new ServerHeaderGlobalMiddleware(next).Handle);

        if (headersConfig.Date)
            chain.Add(next => new DateHeaderGlobalMiddleware(next).Handle);

        if (headersConfig.RequestId)
            chain.Add(next => new RequestIdGlobalMiddleware(next).Handle);

        if (headersConfig.StrictTransportSecurity is { } hsts)
            chain.Add(next => new StrictTransportSecurityGlobalMiddleware(next, hsts).Handle);
    }
}