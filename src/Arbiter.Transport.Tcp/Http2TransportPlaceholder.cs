using System.Net;
using Arbiter.Application.Interfaces;

namespace Arbiter.Transport.Tcp;

public sealed class Http2TransportPlaceholder(Stream stream, bool isSecure, int port, IPAddress? remoteAddress) : ITransport
{
    public Core.Enums.Protocol Protocol => Core.Enums.Protocol.Http2;
    public bool IsSecure => isSecure;
    public int Port => port;
    public IPAddress? RemoteAddress => remoteAddress;

    public async IAsyncEnumerable<ITransportStream> GetStreams([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // HTTP/2 yields multiplexed streams from the same connection
        // This is a stub - full implementation would parse HTTP/2 frames
        throw new NotImplementedException("HTTP/2 transport not yet implemented");
        yield break;
    }

    public Task<ITransport> UpgradeAsync(Core.Enums.Protocol targetProtocol)
        => throw new NotSupportedException("HTTP/2 transport cannot be upgraded");

    public async ValueTask DisposeAsync() => await stream.DisposeAsync();
}
