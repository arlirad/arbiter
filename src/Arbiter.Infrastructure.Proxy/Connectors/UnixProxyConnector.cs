using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Arbiter.Core.Aggregates;

namespace Arbiter.Infrastructure.Proxy.Connectors;

internal sealed class UnixProxyConnector(string socketPath) : IProxyConnector
{
    private readonly string _socketPath = socketPath;

    public SocketsHttpHandler CreateHandler()
    {
        var endpoint = new UnixDomainSocketEndPoint(_socketPath);

        return new SocketsHttpHandler {
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectCallback = async (ctx, token) => {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(endpoint, token);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };
    }

    public Uri BuildTargetUri(string requestPath) => new("http://localhost" + requestPath);

    public string GetHost(Context context) => context.Request.Authority ?? "localhost";

    public async Task<UpgradeConnection> ConnectForUpgradeAsync()
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath));

        return new UpgradeConnection(socket, new NetworkStream(socket));
    }
}