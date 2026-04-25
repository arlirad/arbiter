using System.Net;
using System.Net.Quic;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Arbiter.Application.Interfaces;
using Arlirad.Http3.Enums;

namespace Arlirad.Http3;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3Transport(QuicConnection connection, int port, IPAddress? remoteAddress) : ITransport
{
    public async IAsyncEnumerable<ITransaction> AcceptTransactions([EnumeratorCancellation] CancellationToken ct)
    {
        await using var http3 = new Http3Connection(connection);
        await http3.Start();

        while (true)
        {
            var stream = await http3.GetRequestStream(ct);
            yield return new Http3Transaction(stream, port, remoteAddress);
        }
    }

    public async ValueTask DisposeAsync() => await connection.CloseAsync((long)ErrorCode.InternalError, CancellationToken.None);
}