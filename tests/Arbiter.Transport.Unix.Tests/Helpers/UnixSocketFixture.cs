using System.Net.Sockets;
using System.Reflection;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Middleware;
using Arbiter.Protocol.Http11;

namespace Arbiter.Transport.Unix.Tests.Helpers;

public class UnixSocketFixture(string socketPath, Func<RequestDto, Task<ResponseDto>>? requestHandler = null) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private readonly Func<RequestDto, Task<ResponseDto>> _requestHandler = requestHandler ?? (req => Task.FromResult(new ResponseDto {
        Status = Status.Ok,
        Headers = new ReadOnlyHeaders([]),
    }));

    private UnixSocketAcceptor? _acceptor;
    private HttpClient? _client;
    private Task _serverLoop = null!;
    public string SocketPath
    {
        get;
    } = socketPath;

    public HttpClient Client => _client! ?? throw new InvalidOperationException("Fixture not initialized");

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        _client?.Dispose();

        if (_acceptor is not null)
        {
            if (_acceptor.GetType()
                    .GetField("_sockets", BindingFlags.NonPublic | BindingFlags.Instance)?
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

    private async Task InitializeAsync()
    {
        _acceptor = new UnixSocketAcceptor();
        await _acceptor.Bind([SocketPath], 128);

        await Task.Yield();

        var handler = new SocketsHttpHandler {
            ConnectCallback = async (_, ct) => {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), ct);

                return new NetworkStream(socket, true);
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
        await using var protocol = new Http11Protocol(new TransactionIdProvider());

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

    public static async Task<UnixSocketFixture> CreateAsync(Func<RequestDto, Task<ResponseDto>>? requestHandler = null)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"arbiter_test_{Guid.NewGuid():N}.sock");
        var fixture = new UnixSocketFixture(tempPath, requestHandler);
        await fixture.InitializeAsync();

        return fixture;
    }
}
