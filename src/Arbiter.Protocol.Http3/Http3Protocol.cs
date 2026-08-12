using System.Net.Quic;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Arbiter.Application.Interfaces;
using Arbiter.Infrastructure.Middleware;

namespace Arbiter.Protocol.Http3;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3Protocol(TransactionIdProvider transactionIdProvider) : IProtocol
{
    private Http3Connection? _connection;

    public Http3Connection ServerConnection => _connection!;

    public Http3ConnectionSettings LocalSettings => _connection?.LocalSettings
        ?? throw new InvalidOperationException("Protocol not started");

    public async IAsyncEnumerable<ITransaction> AcceptTransactions(
        IConnection connection,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (connection is not IMultiplexedConnection mux)
            throw new InvalidOperationException("Http3Protocol requires an IMultiplexedConnection");

        _connection = new Http3Connection(mux);

        try
        {
            await _connection.Start();
        }
        catch (QuicException)
        {
            yield break;
        }
        catch (IOException)
        {
            yield break;
        }

        await foreach (var ts in connection.GetStreams(ct))
        {
            if (ts is not IMultiplexedStream ms)
                throw new InvalidOperationException("Expected IMultiplexedStream from IMultiplexedConnection.");

            var requestStream = _connection.FeedInboundStream(ms);

            if (requestStream is null)
                continue;

            yield return new Http3Transaction(transactionIdProvider, requestStream, mux.Port, mux.RemoteAddress);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
