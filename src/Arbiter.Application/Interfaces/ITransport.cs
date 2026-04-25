namespace Arbiter.Application.Interfaces;

public interface ITransport : IAsyncDisposable
{
    IAsyncEnumerable<ITransaction> AcceptTransactions(CancellationToken ct);
}