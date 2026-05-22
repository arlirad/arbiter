using System.Net.Quic;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Arbiter.Application.Interfaces;
using Arbiter.Infrastructure.Middleware;
using Arbiter.Transport.Quic;

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
        ITransport transport,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (transport is not QuicTransport quicTransport)
            throw new InvalidOperationException("Http3Protocol requires QuicTransport");

        _connection = new Http3Connection(quicTransport.Connection);

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

        await foreach (var transportStream in transport.GetStreams(ct))
        {
            var quicStream = (QuicStream)transportStream.Stream;

            var requestStream = _connection.FeedInboundStream(quicStream);

            if (requestStream is null)
                continue;

            yield return new Http3Transaction(transactionIdProvider, requestStream, quicTransport.Port, quicTransport.RemoteAddress);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
