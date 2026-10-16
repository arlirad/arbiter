using Arbiter.Core.Aggregates;

namespace Arbiter.Core.Interfaces;

public delegate Task HandleDelegate(Context context);

public interface IMiddleware
{
    Task Handle(Context context);
}
