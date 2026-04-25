using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using Arbiter.Core.Aggregates;

namespace Arbiter.Infrastructure.Proxy.Connectors;

internal sealed class TcpProxyConnector(Uri target) : IProxyConnector
{
    public SocketsHttpHandler CreateHandler()
    {
        return new SocketsHttpHandler {
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
        };
    }

    public Uri BuildTargetUri(string requestPath)
    {
        var targetPath = target.AbsolutePath.TrimEnd('/') + '/' + requestPath.TrimStart('/');
        return new Uri(target, targetPath);
    }

    public string GetHost(Context context) => target.Host;

    public async Task<UpgradeConnection> ConnectForUpgradeAsync()
    {
        var port = target.IsDefaultPort
            ? target.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80
            : target.Port;

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(target.Host, port);

        Stream stream = new NetworkStream(socket);

        if (!target.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return new UpgradeConnection(socket, stream);

        var ssl = new SslStream(stream, false);
        await ssl.AuthenticateAsClientAsync(target.Host);
        stream = ssl;

        return new UpgradeConnection(socket, stream);
    }
}