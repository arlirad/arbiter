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
        IConnection connection,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (connection is not Arbiter.Transport.Quic.QuicConnection quicConnection)
            throw new InvalidOperationException("Http3Protocol requires QuicConnection");

        _connection = new Http3Connection(quicConnection.InnerConnection);

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

        await foreach (var transportStream in connection.GetStreams(ct))
        {
            var quicStream = (QuicStream)transportStream.Stream;

            var requestStream = _connection.FeedInboundStream(quicStream);

            if (requestStream is null)
                continue;

            yield return new Http3Transaction(transactionIdProvider, requestStream, quicConnection.Port, quicConnection.RemoteAddress);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
