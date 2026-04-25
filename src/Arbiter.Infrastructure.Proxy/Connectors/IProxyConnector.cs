using System.Net.Http;
using Arbiter.Core.Aggregates;

namespace Arbiter.Infrastructure.Proxy.Connectors;

internal interface IProxyConnector
{
    SocketsHttpHandler CreateHandler();
    Uri BuildTargetUri(string requestPath);
    string GetHost(Context context);
    Task<UpgradeConnection> ConnectForUpgradeAsync();
}