using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Arbiter.Infrastructure.Middleware;
using Arbiter.Protocol.Http3;
using Arbiter.Protocol.Http3.Streams;
using Arbiter.Transport.Quic;

namespace Arbiter.Http3.Tests.Helpers;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3IntegrationFixture(X509Certificate2 certificate) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<Http3RequestStream> _requestChannel = Channel.CreateBounded<Http3RequestStream>(new BoundedChannelOptions(256));
    private QuicConnection? _clientConnection;
    private QuicListener? _listener;
    private Http3Protocol? _serverProtocol;

    public int Port
    {
        get;
        private set;
    }
    public Http3Protocol ServerProtocol => _serverProtocol!;
    public QuicConnection ClientConnection => _clientConnection!;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _cts.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
        }

        if (_serverProtocol is not null)
            await _serverProtocol.DisposeAsync();

        if (_clientConnection is not null)
            await _clientConnection.DisposeAsync();

        if (_listener is not null)
            await _listener.DisposeAsync();

        _cts.Dispose();
    }

    public static async Task<Http3IntegrationFixture> CreateAsync()
    {
        if (!QuicListener.IsSupported)
            Assert.Ignore("QUIC is not supported on this platform");

        var certificate = SelfSignedCertificate.Create();
        var fixture = new Http3IntegrationFixture(certificate);
        await fixture.InitializeAsync();

        return fixture;
    }

    private async Task InitializeAsync()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 0);

        _listener = await QuicListener.ListenAsync(new QuicListenerOptions {
            ApplicationProtocols = [SslApplicationProtocol.Http3],
            ListenBacklog = 128,
            ListenEndPoint = endpoint,
            ConnectionOptionsCallback = ConnectionOptionsCallback,
        });

        Port = _listener.LocalEndPoint.Port;

        var clientOptions = new QuicClientConnectionOptions {
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, Port),
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            MaxInboundUnidirectionalStreams = 100,
            MaxInboundBidirectionalStreams = 100,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions {
                TargetHost = "localhost",
                ApplicationProtocols = [SslApplicationProtocol.Http3],
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            },
        };

        _clientConnection = await QuicConnection.ConnectAsync(clientOptions);
        var serverQuicConnection = await _listener.AcceptConnectionAsync(CancellationToken.None);

        var serverTransport = new QuicTransport(serverQuicConnection, Port, null);
        _serverProtocol = new Http3Protocol(new TransactionIdProvider());

        _ = ServeRequests(serverTransport);
    }

    private ValueTask<QuicServerConnectionOptions> ConnectionOptionsCallback(
        QuicConnection connection,
        SslClientHelloInfo clientHello,
        CancellationToken ct)
    {
        var options = new QuicServerConnectionOptions {
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 1,
            MaxInboundUnidirectionalStreams = 100,
            MaxInboundBidirectionalStreams = 100,
            ServerAuthenticationOptions = new SslServerAuthenticationOptions {
                ClientCertificateRequired = false,
                ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls13,
                ApplicationProtocols = [SslApplicationProtocol.Http3],
            },
        };

        return ValueTask.FromResult(options);
    }

    private async Task ServeRequests(QuicTransport serverTransport)
    {
        var ct = _cts.Token;

        try
        {
            await foreach (var transaction in _serverProtocol!.AcceptTransactions(serverTransport, ct))
            {
                if (transaction is Http3Transaction h3tx)
                {
                    await _requestChannel.Writer.WriteAsync(h3tx.GetRequestStream(), ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task<Http3RequestStream> AcceptRequestStream(CancellationToken ct = default)
        => await _requestChannel.Reader.ReadAsync(ct);

    public ValueTask<QuicStream> OpenClientStreamAsync(CancellationToken ct = default)
        => _clientConnection!.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct);

    public async Task<Http3RequestStream> CreateClientRequestStreamAsync(CancellationToken ct = default)
    {
        var stream = await OpenClientStreamAsync(ct);

        return new Http3RequestStream(_serverProtocol!.ServerConnection, stream.Id, stream);
    }
}
