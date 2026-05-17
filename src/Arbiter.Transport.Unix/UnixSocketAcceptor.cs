using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Channels;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;
using Arbiter.Transport.Unix.Models;
using Serilog;

namespace Arbiter.Transport.Unix;

public class UnixSocketAcceptor : IAcceptor, IAsyncConfigurable<UnixListenConfig>, IDisposable
{
    private const int Backlog = 128;

    private readonly ConcurrentDictionary<string, UnixSocketAcceptorSocket> _sockets = new();
    private readonly Channel<ITransport> _transports =
        Channel.CreateBounded<ITransport>(new BoundedChannelOptions(4096));
    private readonly CompositeDisposable _subscriptions = [];
    private readonly SemaphoreSlim _reconfigureLock = new(1, 1);

    public UnixSocketAcceptor(ConfigurationProvider configProvider)
    {
        var unixConfig = configProvider.Observe<Dictionary<string, SiteConfig>>("Sites")
            .Select(sites => {
                var paths = sites.SelectMany(s => s.Value.Bindings ?? [])
                    .Where(b => b.Scheme == "unix").Select(b => b.AbsolutePath).Distinct().ToList();
                return new UnixListenConfig(paths);
            });

        _subscriptions.Add(unixConfig.Subscribe(async config => {
            try
            {
                await _reconfigureLock.WaitAsync();
                try
                {
                    await ReconfigureAsync(config);
                }
                finally
                {
                    _reconfigureLock.Release();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reconfigure Unix acceptor");
            }
        }));
    }

    public async Task<ITransport> Accept(CancellationToken ct)
    {
        while (true)
            return await _transports.Reader.ReadAsync(ct);
    }

    public async ValueTask ReconfigureAsync(UnixListenConfig config) => await Bind(config.Paths);

    public async Task Bind(IEnumerable<string> paths)
    {
        var pathList = paths.ToList();
        await CreateSockets(pathList);
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

    private Task CreateSockets(List<string> paths)
    {
        foreach (var path in paths)
        {
            if (_sockets.ContainsKey(path))
                continue;

            if (File.Exists(path))
                File.Delete(path);

            var endPoint = new UnixDomainSocketEndPoint(path);
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);

            socket.Bind(endPoint);
            socket.Listen(Backlog);

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
        _subscriptions.Dispose();
        _reconfigureLock.Dispose();
    }
}
