using Arbiter.Core.Aggregates;

namespace Arbiter.Application.Interfaces;

public delegate Task GlobalHandleDelegate(ITransaction transaction, Site? site, Context context);

public interface IGlobalMiddleware
{
    Task Handle(ITransaction transaction, Site? site, Context context);
}
