using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Channels;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Protocol.Http11;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Serilog;

namespace Arbiter.Transport.Unix;

public class UnixSocketAcceptor : IAcceptor, IAsyncConfigurable
{
    private const int Backlog = 128;

    private readonly ConcurrentDictionary<string, UnixSocketAcceptorSocket> _sockets = new();

    private readonly Channel<ITransaction> _transactions =
        Channel.CreateBounded<ITransaction>(new BoundedChannelOptions(4096));

    private ConfigurationScope? _scope;

    public async Task<ITransaction> Accept(CancellationToken ct)
    {
        while (true)
            return await _transactions.Reader.ReadAsync(ct);
    }

    public async ValueTask Bind(IConfiguration configuration)
    {
        _scope = new ConfigurationScope(configuration, "Sites");
        await UpdateBindings();
        ChangeToken.OnChange(_scope.GetReloadToken, () => _ = UpdateBindingsAsync());
    }

    private async Task UpdateBindingsAsync()
    {
        try
        {
            await UpdateBindings();
        }
        catch (Exception e)
        {
            Log.Error("Failed to reload config: {Exception}", e);
        }
    }

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

            await using var transport = new Http11Transport(stream, false, -1, null, ct);

            await foreach (var transaction in transport.AcceptTransactions(ct))
                await _transactions.Writer.WriteAsync(transaction, ct);
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

    private async Task UpdateBindings()
    {
        try
        {
            if (_scope is null)
                return;

            var paths = ExtractSocketPaths();

            if (paths is not null)
                await Bind(paths);
        }
        catch (Exception e)
        {
            Log.Error("Failed to reload config: {Exception}", e);
        }
    }

    private IEnumerable<string>? ExtractSocketPaths()
    {
        var sites = _scope?.GetSection("Sites").Get<Dictionary<string, SiteConfig>>();
        if (sites is null)
            return null;

        var paths = sites
            .SelectMany(s => s.Value.Bindings ?? [])
            .Where(uri => uri.Scheme == "unix")
            .Select(uri => uri.AbsolutePath);

        return paths.Distinct().ToList();
    }
}