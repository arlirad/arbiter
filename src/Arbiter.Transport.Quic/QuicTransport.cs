using System.Net;
using System.Net.Quic;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Arbiter.Application;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;

namespace Arbiter.Transport.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public sealed class QuicTransport(QuicConnection connection, int port, IPAddress? remoteAddress) : ITransport
{
    public Protocol Protocol => Protocol.Http3;
    public bool IsSecure => true;
    public int Port => port;
    public IPAddress? RemoteAddress => remoteAddress;
    public QuicConnection Connection => connection;

    public async IAsyncEnumerable<ITransportStream> GetStreams(
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            QuicStream? stream;

            try
            {
                stream = await connection.AcceptInboundStreamAsync(ct);
            }
            catch (QuicException)
            {
                break;
            }

            yield return new TransportStream(stream, stream.Id);
        }
    }

    public Task<ITransport> UpgradeAsync(Protocol targetProtocol)
        => throw new NotSupportedException("QUIC transport cannot be upgraded");

    public async ValueTask DisposeAsync()
        => await connection.CloseAsync(0, CancellationToken.None);
}
