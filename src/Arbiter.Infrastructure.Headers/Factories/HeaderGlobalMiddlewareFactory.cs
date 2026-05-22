using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;

namespace Arbiter.Infrastructure.Headers.Factories;

public class HeaderGlobalMiddlewareFactory : IGlobalMiddlewareFactory
{
    private ServerHeadersConfig? _currentConfig;

    public HeaderGlobalMiddlewareFactory(ConfigurationProvider configProvider)
    {
        configProvider.Observe<ServerHeadersConfig>("headers").Subscribe(config => _currentConfig = config);
    }

    public HandleDelegate Create(HandleDelegate next)
    {
        var config = _currentConfig ?? new ServerHeadersConfig();

        if (config.StrictTransportSecurity is { } hsts)
        {
            var middleware = new StrictTransportSecurityGlobalMiddleware(next, hsts);
            next = middleware.Handle;
        }

        if (config.RequestId)
        {
            var middleware = new RequestIdGlobalMiddleware(next);
            next = middleware.Handle;
        }

        if (config.Date)
        {
            var middleware = new DateHeaderGlobalMiddleware(next);
            next = middleware.Handle;
        }

        if (config.Server)
        {
            var middleware = new ServerHeaderGlobalMiddleware(next);
            next = middleware.Handle;
        }

        return next;
    }
}
