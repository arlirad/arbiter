namespace Arbiter.Application.Middleware;

public sealed class TransactionIdProvider
{
    private int _nextId;

    public int Next() => Interlocked.Increment(ref _nextId);
}