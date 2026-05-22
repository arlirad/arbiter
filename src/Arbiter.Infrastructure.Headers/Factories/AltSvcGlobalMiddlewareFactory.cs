using Arbiter.Application.Interfaces;
using Arbiter.Application.Services;

namespace Arbiter.Infrastructure.Headers.Factories;

public class AltSvcGlobalMiddlewareFactory(AltSvcService altSvc) : IGlobalMiddlewareFactory
{
    public HandleDelegate Create(HandleDelegate next) => altSvc.HeaderValue is not null
        ? new AltSvcGlobalMiddleware(next, altSvc).Handle
        : next;
}
