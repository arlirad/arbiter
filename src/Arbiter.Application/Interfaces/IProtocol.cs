namespace Arbiter.Application.Interfaces;

public interface IProtocol : IAsyncDisposable
{
    IAsyncEnumerable<ITransaction> AcceptTransactions(IConnection connection, CancellationToken ct);
}
