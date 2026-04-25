using Arbiter.Core.Interfaces;

namespace Arbiter.Api;

internal class MiddlewareChainOrchestrator
{
    private HandleDelegate? _next;

    public HandleDelegate GetNext() => _next ?? throw new InvalidOperationException("Next HandleDelegate was not set");

    public void SetNext(HandleDelegate handleDelegate) => _next = handleDelegate;
}