using System.Net;
using System.Runtime.CompilerServices;
using Arbiter.Application;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;

namespace Arbiter.Transport.Unix;

public sealed class UnixConnection(Stream stream, int port, IPAddress? remoteAddress) : IConnection
{
    public Protocol Protocol => Protocol.Http11;
    public bool IsSecure => false;
    public int Port => port;
    public IPAddress? RemoteAddress => remoteAddress;

    public async IAsyncEnumerable<ITransportStream> GetStreams(
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new TransportStream(stream, 0);
    }

    public Task<IConnection> UpgradeAsync(Protocol targetProtocol)
        => throw new NotSupportedException($"Cannot upgrade Unix connection to {targetProtocol}");

    public async ValueTask DisposeAsync() => await stream.DisposeAsync();
}
