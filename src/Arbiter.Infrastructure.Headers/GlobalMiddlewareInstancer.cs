using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;

namespace Arbiter.Infrastructure.Headers;

public class HeaderGlobalMiddlewareInstancer : IHeaderGlobalMiddlewareInstancer
{
    public List<IGlobalMiddleware> Instance(ServerHeadersConfig config, GlobalHandleDelegate next)
    {
        var list = new List<IGlobalMiddleware>();

        if (config.Server)
        {
            var middleware = new ServerHeaderGlobalMiddleware(next);
            next = middleware.Handle;
            list.Add(middleware);
        }

        if (config.Date)
        {
            var middleware = new DateHeaderGlobalMiddleware(next);
            next = middleware.Handle;
            list.Add(middleware);
        }

        if (config.RequestId)
        {
            var middleware = new RequestIdGlobalMiddleware(next);
            next = middleware.Handle;
            list.Add(middleware);
        }

        if (config.StrictTransportSecurity is { } hsts)
        {
            var middleware = new StrictTransportSecurityGlobalMiddleware(next, hsts);
            next = middleware.Handle;
            list.Add(middleware);
        }

        return list;
    }
}
