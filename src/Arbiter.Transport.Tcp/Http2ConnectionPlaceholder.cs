using System.Net;
using System.Runtime.CompilerServices;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;

namespace Arbiter.Transport.Tcp;

public sealed class Http2ConnectionPlaceholder(Stream stream, bool isSecure, int port, IPAddress? remoteAddress) : IConnection
{
    public Protocol Protocol => Protocol.Http2;
    public bool IsSecure => isSecure;
    public int Port => port;
    public IPAddress? RemoteAddress => remoteAddress;

    public async IAsyncEnumerable<ITransportStream> GetStreams([EnumeratorCancellation] CancellationToken ct)
    {
        throw new NotImplementedException("HTTP/2 connection not yet implemented");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    public Task<IConnection> UpgradeAsync(Protocol targetProtocol)
        => throw new NotSupportedException("HTTP/2 connection cannot be upgraded");

    public async ValueTask DisposeAsync() => await stream.DisposeAsync();
}
