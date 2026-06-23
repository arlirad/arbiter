using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Channels;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;
using Arbiter.Core.Enums;
using Arbiter.Transport.Unix.Configuration;
using Serilog;

namespace Arbiter.Transport.Unix;

public class UnixSocketTransport : ITransport, IAsyncConfigurable<UnixTransportConfig, HashSet<Protocol>>, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "unix");
    private readonly ConcurrentDictionary<string, UnixSocketTransportSocket> _sockets = new();
    private Channel<IConnection>? _connections;

    public async Task<IConnection> Accept(CancellationToken ct) => await _connections!.Reader.ReadAsync(ct);

    public async ValueTask ReconfigureAsync(UnixTransportConfig config, HashSet<Protocol> protocols)
    {
        _connections ??= Channel.CreateBounded<IConnection>(new BoundedChannelOptions(config.QueueSize));

        if (config.Paths is null || config.Paths.Count == 0)
            Log.Warning("No paths configured");

        await Bind(config.Paths, config.Backlog);
    }

    public void Dispose()
    {
        foreach (var socket in _sockets.Values)
        {
            socket.Stop();
            socket.Close();
        }

        _sockets.Clear();
    }

    public async Task Bind(IEnumerable<string> paths, int backlog)
    {
        _connections ??= Channel.CreateBounded<IConnection>(new BoundedChannelOptions(4096));
        var pathList = paths.ToList();
        await CreateSockets(pathList, backlog);
        await PruneSockets(pathList);
    }

    private async Task AcceptLoop(Socket socket, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var connection = await socket.AcceptAsync(ct);
                _ = ConnectionLoop(connection, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.Error("AcceptLoop error: {Exception}", e);
        }
    }

    private async Task ConnectionLoop(Socket socket, CancellationToken ct)
    {
        try
        {
            var stream = new NetworkStream(socket, false);
            var connection = new UnixConnection(stream, -1, null);
            await _connections.Writer.WriteAsync(connection, ct);
        }
        catch (OperationCanceledException)
        {
            socket.Dispose();
        }
        catch (Exception e)
        {
            Log.Error("Connection loop error: {Exception}", e);
            socket.Dispose();
        }
    }

    private Task CreateSockets(List<string> paths, int backlog)
    {
        var newPaths = paths.Where(p => !_sockets.ContainsKey(p)).ToList();

        if (newPaths.Count > 0)
            Log.Information("Binding {Count} path(s): {Paths}", newPaths.Count, newPaths);

        foreach (var path in newPaths)
        {
            if (File.Exists(path))
                File.Delete(path);

            var endPoint = new UnixDomainSocketEndPoint(path);
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);

            socket.Bind(endPoint);
            socket.Listen(backlog);

            var transportSocket = new UnixSocketTransportSocket(socket, path);

            _sockets[path] = transportSocket;
            _ = AcceptLoop(socket, transportSocket.CancellationToken);
        }

        return Task.CompletedTask;
    }

    private async Task PruneSockets(List<string> paths)
    {
        var toRemove = _sockets.Keys
            .Where(p => !paths.Contains(p))
            .ToList();

        if (toRemove.Count > 0)
            Log.Information("Pruning {Count} path(s): {Paths}", toRemove.Count, toRemove);

        var cancellationTasks = new List<Task>();

        foreach (var path in toRemove)
        {
            if (_sockets.TryRemove(path, out var socket))
            {
                cancellationTasks.Add(socket.Stop());
                _ = Task.Run(socket.Close);
            }
        }

        if (cancellationTasks.Count > 0)
            await Task.WhenAll(cancellationTasks);
    }
}
