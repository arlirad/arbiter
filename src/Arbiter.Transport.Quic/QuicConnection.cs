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
public sealed class QuicConnection(System.Net.Quic.QuicConnection quicConnection, int port, IPAddress? remoteAddress) : IConnection
{
    public System.Net.Quic.QuicConnection InnerConnection => quicConnection;
    public Protocol Protocol => Protocol.Http3;
    public bool IsSecure => true;
    public int Port => port;
    public IPAddress? RemoteAddress => remoteAddress;

    public async IAsyncEnumerable<ITransportStream> GetStreams(
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            QuicStream? stream;

            try
            {
                stream = await quicConnection.AcceptInboundStreamAsync(ct);
            }
            catch (QuicException)
            {
                break;
            }

            yield return new TransportStream(stream, stream.Id);
        }
    }

    public Task<IConnection> UpgradeAsync(Protocol targetProtocol)
        => throw new NotSupportedException("QUIC connection cannot be upgraded");

    public async ValueTask DisposeAsync()
        => await quicConnection.CloseAsync(0, CancellationToken.None);
}
