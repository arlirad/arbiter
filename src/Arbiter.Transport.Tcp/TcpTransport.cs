using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using Arbiter.Application;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;

namespace Arbiter.Transport.Tcp;

public sealed class TcpTransport(Stream stream, bool isSecure, int port, IPAddress? remoteAddress) : ITransport
{
    public Protocol Protocol
    {
        get {
            if (isSecure && stream is SslStream ssl)
            {
                var protocol = ssl.NegotiatedApplicationProtocol;

                if (protocol == SslApplicationProtocol.Http2)
                    return Protocol.Http2;
            }

            return Protocol.Http11;
        }
    }

    public bool IsSecure => isSecure;
    public int Port => port;
    public IPAddress? RemoteAddress => remoteAddress;

    public async IAsyncEnumerable<ITransportStream> GetStreams([EnumeratorCancellation] CancellationToken ct)
    {
        yield return new TransportStream(stream, 0);
    }

    public Task<ITransport> UpgradeAsync(Protocol targetProtocol)
        => targetProtocol == Protocol.Http2
            ? Task.FromResult<ITransport>(new Http2TransportPlaceholder(stream, IsSecure, Port, RemoteAddress))
            : throw new NotSupportedException($"Cannot upgrade TCP transport to {targetProtocol}");

    public async ValueTask DisposeAsync() => await stream.DisposeAsync();
}
