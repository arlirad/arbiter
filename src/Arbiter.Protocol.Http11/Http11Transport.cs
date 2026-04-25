using System.Net;
using System.Runtime.CompilerServices;
using Arbiter.Application.Interfaces;

namespace Arbiter.Protocol.Http11;

public class Http11Transport(Stream stream, bool isSecure, int port, IPAddress? remoteAddress, CancellationToken ct) : ITransport
{
    public async IAsyncEnumerable<ITransaction> AcceptTransactions([EnumeratorCancellation] CancellationToken ct)
    {
        while (true)
        {
            var transaction = new Http11Transaction(stream, isSecure, port, remoteAddress, ct);
            yield return transaction;
            await transaction.ResponseSet;

            if (transaction.Upgraded)
            {
                await transaction.UpgradeCompleted;
                yield break;
            }

            if (transaction.Finished || transaction.Faulted)
                yield break;
        }
    }

    public async ValueTask DisposeAsync() => await stream.DisposeAsync();
}