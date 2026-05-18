using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;
using Arbiter.Core.Enums;
using Serilog;

namespace Arbiter.Transport.Unix;

public class UnixSocketAcceptor : IAcceptor, IAsyncConfigurable<UnixTransportConfig, HashSet<Protocol>>, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "unix");
    private readonly ConcurrentDictionary<string, UnixSocketAcceptorSocket> _sockets = new();
    private Channel<ITransport>? _transports;

    public UnixSocketAcceptor()
    {
    }

    public async Task<ITransport> Accept(CancellationToken ct) => await _transports!.Reader.ReadAsync(ct);

    public async ValueTask ReconfigureAsync(UnixTransportConfig config, HashSet<Protocol> protocols)
    {
        _transports ??= Channel.CreateBounded<ITransport>(new BoundedChannelOptions(config.QueueSize));

        if (config.Paths is null || config.Paths.Count == 0)
            Log.Warning("No paths configured");

        await Bind(config.Paths, config.Backlog);
    }

    public async Task Bind(IEnumerable<string> paths, int backlog)
    {
        _transports ??= Channel.CreateBounded<ITransport>(new BoundedChannelOptions(4096));
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
            var stream = new NetworkStream(socket, ownsSocket: false);
            var transport = new UnixTransport(stream, -1, null);
            await _transports.Writer.WriteAsync(transport, ct);
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

            var acceptorSocket = new UnixSocketAcceptorSocket(socket, path);

            _sockets[path] = acceptorSocket;
            _ = AcceptLoop(socket, acceptorSocket.CancellationToken);
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

    public void Dispose()
    {
        foreach (var socket in _sockets.Values)
        {
            socket.Stop();
            socket.Close();
        }

        _sockets.Clear();
    }
}
