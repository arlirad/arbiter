using System.Net;
using System.Net.Http;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;
using Arbiter.Transport.Quic;
using Arlirad.Http3;
using Arlirad.Http3.Streams;

namespace Arbiter.Http3.Tests.Helpers;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class HttpClientServerFixture : IAsyncDisposable
{
    private readonly X509Certificate2 _certificate;
    private readonly CancellationTokenSource _cts = new();
    private readonly QuicListener _listener;
    private readonly Func<RequestDto, Task<ResponseDto>> _requestHandler;
    private Task _serverLoop = null!;

    private HttpClientServerFixture(QuicListener listener, X509Certificate2 certificate, Func<RequestDto, Task<ResponseDto>> requestHandler)
    {
        _listener = listener;
        _certificate = certificate;
        _requestHandler = requestHandler;

        var handler = new SocketsHttpHandler {
            SslOptions = new SslClientAuthenticationOptions {
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
                ApplicationProtocols = [SslApplicationProtocol.Http3],
            },
        };

        Client = new HttpClient(handler) {
            BaseAddress = new Uri($"https://127.0.0.1:{Port}"),
            DefaultRequestVersion = System.Net.HttpVersion.Version30,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
    }

    public int Port => _listener.LocalEndPoint.Port;
    public HttpClient Client { get; } = null!;

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        Client.Dispose();

        try
        {
            await _listener.DisposeAsync();
        }
        catch { }

        try
        {
            await (_serverLoop ?? Task.CompletedTask);
        }
        catch { }

        _cts.Dispose();
    }

    public static async Task<HttpClientServerFixture> CreateAsync(Func<RequestDto, Task<ResponseDto>>? requestHandler = null)
    {
        if (!QuicListener.IsSupported)
            Assert.Ignore("QUIC is not supported on this platform");

        var certificate = SelfSignedCertificate.Create("localhost");

        var listener = await QuicListener.ListenAsync(new QuicListenerOptions {
            ApplicationProtocols = [SslApplicationProtocol.Http3],
            ListenBacklog = 128,
            ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 1,
                MaxInboundUnidirectionalStreams = 100,
                MaxInboundBidirectionalStreams = 100,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions {
                    ClientCertificateRequired = false,
                    ServerCertificate = certificate,
                    ApplicationProtocols = [SslApplicationProtocol.Http3],
                },
            }),
        });

        var handler = requestHandler ?? (req => Task.FromResult(new ResponseDto { Status = Status.Ok }));
        var fixture = new HttpClientServerFixture(listener, certificate, handler);
        await fixture.StartServerLoopAsync();
        return fixture;
    }

    private async Task StartServerLoopAsync()
    {
        var ct = _cts.Token;

        _serverLoop = Task.Run(async () => {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var connection = await _listener.AcceptConnectionAsync(ct);
                        _ = HandleConnectionAsync(connection, ct);
                    }
                    catch (QuicException) { }
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task HandleConnectionAsync(QuicConnection connection, CancellationToken ct)
    {
        var transport = new QuicTransport(connection, Port, null);
        await using var protocol = new Http3Protocol();

        var tasks = new List<Task>();

        await foreach (var transaction in protocol.AcceptTransactions(transport, ct))
            tasks.Add(HandleTransactionAsync(transaction));

        await Task.WhenAll(tasks);
    }

    private async Task HandleTransactionAsync(ITransaction transaction)
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
        catch (Exception) { }
    }
}
