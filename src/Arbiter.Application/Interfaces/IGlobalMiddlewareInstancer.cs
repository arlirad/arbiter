using Arbiter.Application.Configuration;

namespace Arbiter.Application.Interfaces;

public interface IHeaderGlobalMiddlewareInstancer
{
    List<IGlobalMiddleware> Instance(ServerHeadersConfig config, GlobalHandleDelegate next);
}
