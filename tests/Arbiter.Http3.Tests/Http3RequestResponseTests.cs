using System.Net.Quic;
using System.Runtime.Versioning;
using System.Text;
using Arbiter.Http3.Tests.Helpers;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3RequestResponseTests
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
    public async Task GET_request_reaches_server()
    {
        var fixture = _fixture!;
        var requestStream = await fixture.CreateClientRequestStreamAsync();

        await requestStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["GET"],
            [":path"] = ["/"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });
        await requestStream.FinishAsync();

        var serverStream = await fixture.AcceptRequestStream(default);
        var hasHeaders = false;

        await foreach (var _ in serverStream.ReadHeaders(CancellationToken.None))
            hasHeaders = true;

        Assert.That(hasHeaders, Is.True);
    }

    [Test]
    public async Task POST_with_body_reaches_server()
    {
        var fixture = _fixture!;
        var requestBody = "Hello, HTTP/3!"u8.ToArray();

        var requestStream = await fixture.CreateClientRequestStreamAsync();

        await requestStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["POST"],
            [":path"] = ["/submit"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });
        await requestStream.WriteAsync(requestBody);
        await requestStream.FinishAsync();

        var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);

        await foreach (var _ in serverStream.ReadHeaders())
        {
        }

        var buffer = new byte[100];
        var bytesRead = await serverStream.ReadAsync(buffer);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bytesRead, Is.EqualTo(requestBody.Length));
            Assert.That(Encoding.UTF8.GetString(buffer, 0, bytesRead), Is.EqualTo("Hello, HTTP/3!"));
        }
    }
}