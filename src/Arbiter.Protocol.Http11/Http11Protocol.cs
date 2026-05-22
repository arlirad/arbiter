using System.Runtime.CompilerServices;
using Arbiter.Application.Interfaces;
using Arbiter.Infrastructure.Middleware;

namespace Arbiter.Protocol.Http11;

public class Http11Protocol(TransactionIdProvider transactionIdProvider) : IProtocol
{
    public async IAsyncEnumerable<ITransaction> AcceptTransactions(
        ITransport transport,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var transportStream in transport.GetStreams(ct))
        {
            var stream = transportStream.Stream;
            var isSecure = transport.IsSecure;
            var port = transport.Port;
            var remoteAddress = transport.RemoteAddress;

            while (true)
            {
                var transaction = new Http11Transaction(transactionIdProvider, stream, isSecure, port, remoteAddress, ct);

                yield return transaction;

                await transaction.ResponseSet;

                if (transaction.Upgraded)
                {
                    await transaction.UpgradeCompleted;

                    break;
                }

                if (transaction.Finished || transaction.Faulted)
                    break;
            }
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
