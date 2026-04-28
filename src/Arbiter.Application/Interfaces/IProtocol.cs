using Arbiter.Core.Enums;

namespace Arbiter.Application.Interfaces;

public interface IProtocol : IAsyncDisposable
{
    IAsyncEnumerable<ITransaction> AcceptTransactions(ITransport transport, CancellationToken ct);
}
