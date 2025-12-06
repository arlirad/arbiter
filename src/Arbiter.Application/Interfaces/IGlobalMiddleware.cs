using Arbiter.Domain.Aggregates;

namespace Arbiter.Application.Interfaces;

public delegate Task HandleDelegate(ITransaction transaction, Site? site, Context context);

public interface IGlobalMiddleware
{
    public Task Handle(ITransaction transaction, Site? site, Context context);
}