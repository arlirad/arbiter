using System.Net.Sockets;

namespace Arbiter.Infrastructure.Proxy.Connectors;

internal sealed class UpgradeConnection(Socket socket, Stream stream) : IDisposable
{
    private Socket? _socket = socket;

    public Stream Stream
    {
        get;
    } = stream;

    public void Dispose() => _socket?.Dispose();

    public void Detach() => _socket = null;
}