using Arbiter.Middleware;
using Arbiter.Models.Network;

namespace Arbiter.Factories;

internal class MiddlewareChainDelegateFactory
{
    private HandleDelegate? _next;

    public HandleDelegate GetNext()
    {
        return _next ?? throw new NullReferenceException("Next HandleDelegate was null");
    }

    public void SetNext(HandleDelegate handleDelegate)
    {
        _next = handleDelegate;
    }
}