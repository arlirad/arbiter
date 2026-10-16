using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Arbiter.Protocol.Http3;

namespace Arbiter.Http3.Tests.Helpers;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class RawQuicFixture : IAsyncDisposable
{
    private readonly X509Certificate2 _certificate;
    private QuicConnection? _clientQuicConnection;
    private QuicListener? _listener;
    private QuicConnection? _serverQuicConnection;

    private RawQuicFixture(X509Certificate2 certificate)
    {
        _certificate = certificate;
    }

    public QuicConnection ServerQuicConnection => _serverQuicConnection!;
    public QuicConnection ClientQuicConnection => _clientQuicConnection!;
    public int Port
    {
        get;
        private set;
    }

    public async ValueTask DisposeAsync()
    {
        if (_clientQuicConnection is not null)
        {
            try
            {
                await _clientQuicConnection.CloseAsync(0);
            }
            catch
            {
            }
        }

        if (_serverQuicConnection is not null)
        {
            try
            {
                await _serverQuicConnection.CloseAsync(0);
            }
            catch
            {
            }
        }

        if (_listener is not null)
            await _listener.DisposeAsync();
    }

    public static async Task<RawQuicFixture> CreateAsync()
    {
        if (!QuicListener.IsSupported)
            Assert.Ignore("QUIC is not supported on this platform");

        var certificate = SelfSignedCertificate.Create();
        var fixture = new RawQuicFixture(certificate);
        await fixture.InitializeAsync();

        return fixture;
    }

    private async Task InitializeAsync()
    {
        var cert = _certificate;

        _listener = await QuicListener.ListenAsync(new QuicListenerOptions {
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
                    ServerCertificate = cert,
                    EnabledSslProtocols = SslProtocols.Tls13,
                    ApplicationProtocols = [SslApplicationProtocol.Http3],
                },
            }),
        });

        Port = _listener.LocalEndPoint.Port;

        var connectTask = QuicConnection.ConnectAsync(new QuicClientConnectionOptions {
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
        }).AsTask();

        var acceptTask = _listener.AcceptConnectionAsync(CancellationToken.None).AsTask();

        await Task.WhenAll(connectTask, acceptTask);

        _clientQuicConnection = await connectTask;
        _serverQuicConnection = await acceptTask;
    }

    public Http3Connection CreateServerHttp3Connection() => new(new Arbiter.Transport.Quic.QuicConnection(_serverQuicConnection!, Port, null));

    public async Task<QuicStream> OpenClientUnidirectionalStreamAsync()
    {
        var stream = await _clientQuicConnection!.OpenOutboundStreamAsync(QuicStreamType.Unidirectional);
        Console.Error.WriteLine($"OpenClientUnidirectionalStreamAsync: stream={stream}, Type={stream.Type}, CanWrite={stream.CanWrite}");

        return stream;
    }

    public static void FeedInboundStream(QuicStream stream, Http3Connection connection)
        => connection.FeedInboundStream(new Arbiter.Transport.Quic.QuicMultiplexedStream(stream));

    public async Task<QuicStream> AcceptServerInboundStream(CancellationToken ct = default)
    {
        var stream = await _serverQuicConnection!.AcceptInboundStreamAsync(ct);
        Console.Error.WriteLine($"AcceptServerInboundStream: stream={stream}, Type={stream.Type}, CanRead={stream.CanRead}");

        return stream;
    }
}
