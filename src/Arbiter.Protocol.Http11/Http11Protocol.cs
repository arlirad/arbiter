using Arbiter.Application.Interfaces;

namespace Arbiter.Protocol.Http11;

public class Http11Protocol : IProtocol
{
    public async IAsyncEnumerable<ITransaction> AcceptTransactions(
        ITransport transport,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var transportStream in transport.GetStreams(ct))
        {
            var stream = transportStream.Stream;
            var isSecure = transport.IsSecure;
            var port = transport.Port;
            var remoteAddress = transport.RemoteAddress;

            while (true)
            {
                var transaction = new Http11Transaction(stream, isSecure, port, remoteAddress, ct);
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
