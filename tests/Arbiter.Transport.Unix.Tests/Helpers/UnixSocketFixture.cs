using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;
using Arbiter.Transport.Unix;

namespace Arbiter.Transport.Unix.Tests.Helpers;

public class UnixSocketFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    public string SocketPath
    {
        get;
    }
    private UnixSocketAcceptor? _acceptor;
    private readonly Func<RequestDto, Task<ResponseDto>> _requestHandler;
    private Task _serverLoop = null!;
    private HttpClient? _client;

    public UnixSocketFixture(string socketPath, Func<RequestDto, Task<ResponseDto>>? requestHandler = null)
    {
        SocketPath = socketPath;
        _requestHandler = requestHandler is not null
            ? requestHandler
            : (req => Task.FromResult(new ResponseDto {
                Status = Status.Ok,
                Headers = new Core.ValueObjects.ReadOnlyHeaders(new Core.ValueObjects.Headers()),
            }));
    }

    public HttpClient Client => _client! ?? throw new InvalidOperationException("Fixture not initialized");

    public async Task InitializeAsync()
    {
        _acceptor = new UnixSocketAcceptor();
        await _acceptor.Bind([SocketPath]);

        await Task.Yield();

        var handler = new SocketsHttpHandler {
            ConnectCallback = async (context, ct) => {
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
                        var transaction = await _acceptor.Accept(ct);
                        _ = HandleTransaction(transaction);
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
            var sockets = _acceptor.GetType()
                .GetField("_sockets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .GetValue(_acceptor) as IDictionary<string, object>;

            if (sockets is not { } dict)
                return;

            foreach (var socketObj in dict.Values)
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