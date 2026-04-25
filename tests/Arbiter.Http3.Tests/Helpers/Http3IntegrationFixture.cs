using System.IO;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Arlirad.Http3;
using Arlirad.Http3.Streams;

namespace Arbiter.Http3.Tests.Helpers;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3IntegrationFixture(X509Certificate2 certificate) : IAsyncDisposable
{
    private readonly X509Certificate2 _certificate = certificate;
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<Http3RequestStream> _requestChannel = Channel.CreateBounded<Http3RequestStream>(new BoundedChannelOptions(256));
    private Http3Connection? _clientConnection;
    private QuicListener? _listener;
    private Http3Connection? _serverConnection;

    public int Port
    {
        get;
        private set;
    }
    public Http3Connection ServerConnection => _serverConnection!;
    public Http3Connection ClientConnection => _clientConnection!;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _cts.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
        }

        if (_serverConnection is not null)
            await _serverConnection.DisposeAsync();

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

        var certificate = SelfSignedCertificate.Create("localhost");
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

        var clientQuicConnection = await QuicConnection.ConnectAsync(clientOptions);

        var serverQuicConnection = await _listener!.AcceptConnectionAsync(CancellationToken.None);

        _serverConnection = new Http3Connection(serverQuicConnection);
        _clientConnection = new Http3Connection(clientQuicConnection);

        await Task.WhenAll(_serverConnection.Start(), _clientConnection.Start());

        _ = ServeRequests();
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
                ServerCertificate = _certificate,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls13,
                ApplicationProtocols = [SslApplicationProtocol.Http3],
            },
        };

        return ValueTask.FromResult(options);
    }

    private async Task ServeRequests()
    {
        var ct = _cts.Token;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var stream = await _serverConnection!.GetRequestStream(ct);
                await _requestChannel.Writer.WriteAsync(stream, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task<Http3RequestStream> AcceptRequestStream(CancellationToken ct = default) => await _requestChannel.Reader.ReadAsync(ct);

    public async Task<QuicStream> OpenClientStreamAsync(CancellationToken ct = default)
    {
        var clientQuic = GetClientQuicConnection();
        return await clientQuic.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct);
    }

    public async Task<Http3RequestStream> CreateClientRequestStreamAsync(CancellationToken ct = default)
    {
        var stream = await OpenClientStreamAsync(ct);
        return new Http3RequestStream(_clientConnection!, stream.Id, stream);
    }

    public QuicConnection GetClientQuicConnection()
    {
        var field = typeof(Http3Connection).GetField("<connection>P",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? typeof(Http3Connection).GetField("_connection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("Cannot find QUIC connection field on Http3Connection");

        return field.GetValue(_clientConnection) as QuicConnection
            ?? throw new InvalidOperationException("Cannot access client QUIC connection");
    }
}