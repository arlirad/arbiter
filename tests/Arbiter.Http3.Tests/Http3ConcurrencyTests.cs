using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using Arbiter.Http3.Tests.Helpers;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3ConcurrencyTests
{
    private Http3IntegrationFixture? _fixture;

    [SetUp]
    public async Task SetUp()
    {
        if (!QuicListener.IsSupported)
            Assert.Ignore("QUIC is not supported on this platform");

        _fixture = await Http3IntegrationFixture.CreateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_fixture is not null)
        {
            try
            {
                await _fixture.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }

            _fixture = null;
        }
    }

    [Test]
    public async Task Sequential_requests_processed()
    {
        var fixture = _fixture!;

        for (var i = 0; i < 3; i++)
        {
            var requestStream = await fixture.CreateClientRequestStreamAsync();

            await requestStream.WriteHeaders(new Dictionary<string, List<string>> {
                [":method"] = ["GET"],
                [":path"] = ["/" + i],
                [":scheme"] = ["https"],
                [":authority"] = ["localhost"],
            });

            await requestStream.FinishAsync();
        }

        for (var i = 0; i < 3; i++)
        {
            var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);

            foreach (var _ in await serverStream.ReadHeaders(CancellationToken.None))
            {
            }

            await serverStream.FinishAsync();
        }

        Assert.Pass();
    }

    [Test]
    public async Task Multiple_connections_accepted()
    {
        var fixture = _fixture!;
        var connections = new List<QuicConnection>();

        for (var i = 0; i < 3; i++)
        {
            var conn = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions {
                RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, fixture.Port),
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                ClientAuthenticationOptions = new SslClientAuthenticationOptions {
                    TargetHost = "localhost",
                    ApplicationProtocols = [SslApplicationProtocol.Http3],
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                },
            });

            connections.Add(conn);
        }

        try
        {
            Assert.That(connections, Has.Count.EqualTo(3));
        }
        finally
        {
            foreach (var conn in connections)
            {
                try
                {
                    await conn.CloseAsync(0, CancellationToken.None);
                }
                catch
                {
                }
            }
        }
    }
}
