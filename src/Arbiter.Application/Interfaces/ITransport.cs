using System.Net;
using Arbiter.Core.Enums;

namespace Arbiter.Application.Interfaces;

public interface ITransport : IAsyncDisposable
{
    Protocol Protocol
    {
        get;
    }
    bool IsSecure
    {
        get;
    }
    int Port
    {
        get;
    }
    IPAddress? RemoteAddress
    {
        get;
    }

    IAsyncEnumerable<ITransportStream> GetStreams(CancellationToken ct);
    Task<ITransport> UpgradeAsync(Protocol targetProtocol);
}
