using Arbiter.Domain.Interfaces;

namespace Arbiter.Application.Orchestrators;

internal class MiddlewareChainDelegateOrchestrator
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