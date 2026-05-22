using Arbiter.Application.Interfaces;

namespace Arbiter.Application.Middleware;

public class CoreGlobalMiddlewareFactory : IGlobalMiddlewareFactory
{
    public HandleDelegate Create(HandleDelegate next)
    {
        var nullSite = new NullSiteGlobalMiddleware(next);
        var exceptionCatcher = new ExceptionCatcherGlobalMiddleware(nullSite.Handle);

        return exceptionCatcher.Handle;
    }
}
