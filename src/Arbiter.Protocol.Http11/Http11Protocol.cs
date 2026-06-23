using System.Runtime.CompilerServices;
using Arbiter.Application.Interfaces;
using Arbiter.Infrastructure.Middleware;

namespace Arbiter.Protocol.Http11;

public class Http11Protocol(TransactionIdProvider transactionIdProvider) : IProtocol
{
    public async IAsyncEnumerable<ITransaction> AcceptTransactions(
        IConnection connection,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var transportStream in connection.GetStreams(ct))
        {
            var stream = transportStream.Stream;
            var isSecure = connection.IsSecure;
            var port = connection.Port;
            var remoteAddress = connection.RemoteAddress;

            while (true)
            {
                var transaction = new Http11Transaction(transactionIdProvider, stream, isSecure, port, remoteAddress);

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
