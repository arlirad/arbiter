using System.Net;
using Arbiter.Application;
using Arbiter.Application.Interfaces;

namespace Arbiter.Transport.Unix;

public sealed class UnixTransport(Stream stream, int port, IPAddress? remoteAddress) : ITransport
{
    public Core.Enums.Protocol Protocol => Core.Enums.Protocol.Http11;
    public bool IsSecure => false;
    public int Port => port;
    public IPAddress? RemoteAddress => remoteAddress;

    public async IAsyncEnumerable<ITransportStream> GetStreams(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return new TransportStream(stream, 0);
    }

    public Task<ITransport> UpgradeAsync(Core.Enums.Protocol targetProtocol)
        => throw new NotSupportedException($"Cannot upgrade Unix transport to {targetProtocol}");

    public async ValueTask DisposeAsync() => await stream.DisposeAsync();
}
