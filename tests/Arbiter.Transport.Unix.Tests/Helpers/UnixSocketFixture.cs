using System.Net.Sockets;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Configuration;
using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;
using Arbiter.Protocol.Http11;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Transport.Unix.Tests.Helpers;

public class UnixSocketFixture(string socketPath, Func<RequestDto, Task<ResponseDto>>? requestHandler = null) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    public string SocketPath
    {
        get;
    } = socketPath;
    private UnixSocketAcceptor? _acceptor;
    private readonly Func<RequestDto, Task<ResponseDto>> _requestHandler = requestHandler ?? (req => Task.FromResult(new ResponseDto {
        Status = Status.Ok,
        Headers = new ReadOnlyHeaders([]),
    }));
    private Task _serverLoop = null!;
    private HttpClient? _client;

    public HttpClient Client => _client! ?? throw new InvalidOperationException("Fixture not initialized");

    private async Task InitializeAsync()
    {
        _acceptor = new UnixSocketAcceptor(new Arbiter.Configuration.ConfigurationProvider(new ConfigurationBuilder().Build()));
        await _acceptor.Bind([SocketPath]);

        await Task.Yield();

        var handler = new SocketsHttpHandler {
            ConnectCallback = async (_, ct) => {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), ct);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };

        _client = new HttpClient(handler) {
            BaseAddress = new Uri("http://localhost"),
        };

        _serverLoop = Task.Run(async () => {
            var ct = _cts.Token;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var transport = await _acceptor.Accept(ct);
                        _ = HandleConnection(transport, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private async Task HandleConnection(ITransport transport, CancellationToken ct)
    {
        await using var protocol = new Http11Protocol();

        await foreach (var transaction in protocol.AcceptTransactions(transport, ct))
        {
            _ = HandleTransaction(transaction);
        }
    }

    private async Task HandleTransaction(ITransaction transaction)
    {
        try
        {
            var request = await transaction.GetRequest();
            if (request is not null)
            {
                var response = await _requestHandler(request);
                await transaction.SetResponse(response);
            }
        }
        catch
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        _client?.Dispose();

        if (_acceptor is not null)
        {
            if (_acceptor.GetType()
                    .GetField("_sockets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .GetValue(_acceptor) is not IDictionary<string, object> sockets)
                return;

            foreach (var socketObj in sockets.Values)
            {
                var stopMethod = socketObj.GetType().GetMethod("Stop");
                var closeMethod = socketObj.GetType().GetMethod("Close");
                if (stopMethod is not null)
                    await (Task)stopMethod.Invoke(socketObj, null)!;
                closeMethod?.Invoke(socketObj, null);
            }
        }

        if (File.Exists(SocketPath))
        {
            try
            {
                File.Delete(SocketPath);
            }
            catch
            {
            }
        }

        _cts.Dispose();
    }

    public static async Task<UnixSocketFixture> CreateAsync(Func<RequestDto, Task<ResponseDto>>? requestHandler = null)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"arbiter_test_{Guid.NewGuid():N}.sock");
        var fixture = new UnixSocketFixture(tempPath, requestHandler);
        await fixture.InitializeAsync();
        return fixture;
    }
}