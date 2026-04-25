using System.Collections.Generic;
using System.Net.Quic;
using System.Runtime.Versioning;
using System.Text;
using Arbiter.Http3.Tests.Helpers;
using Arlirad.Http3.Streams;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3ServerResponseTests
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
    public async Task Server_response_headers_read_by_client()
    {
        var fixture = _fixture!;
        var clientStream = await fixture.CreateClientRequestStreamAsync();

        await clientStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["GET"],
            [":path"] = ["/"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });
        clientStream.Finish();

        var serverStream = await fixture.AcceptRequestStream(default);
        await foreach (var _ in serverStream.ReadHeaders(default))
        {
        }

        await serverStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":status"] = ["200"],
            ["content-type"] = ["text/plain"],
        }, default);
        serverStream.Finish();

        var responseHeaders = new List<KeyValuePair<string, string?>>();
        await foreach (var header in clientStream.ReadHeaders(default))
            responseHeaders.Add(header);

        Assert.Multiple(() => {
            Assert.That(responseHeaders.Any(h => h.Key == ":status" && h.Value == "200"), Is.True);
            Assert.That(responseHeaders.Any(h => h.Key == "content-type" && h.Value == "text/plain"), Is.True);
        });
    }

    [Test]
    public async Task Server_response_with_body_read_by_client()
    {
        var fixture = _fixture!;
        var clientStream = await fixture.CreateClientRequestStreamAsync();
        var responseBody = "Hello from server!"u8.ToArray();

        await clientStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["GET"],
            [":path"] = ["/"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });
        clientStream.Finish();

        var serverStream = await fixture.AcceptRequestStream(default);
        await foreach (var _ in serverStream.ReadHeaders(default))
        {
        }

        await serverStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":status"] = ["200"],
            ["content-type"] = ["text/plain"],
        }, CancellationToken.None);
        await serverStream.WriteAsync(responseBody, CancellationToken.None);
        serverStream.Finish();

        await foreach (var _ in clientStream.ReadHeaders(CancellationToken.None))
        {
        }

        var buffer = new byte[256];
        var bytesRead = await clientStream.ReadAsync(buffer, CancellationToken.None);

        Assert.Multiple(() => {
            Assert.That(bytesRead, Is.EqualTo(responseBody.Length));
            Assert.That(Encoding.UTF8.GetString(buffer, 0, bytesRead), Is.EqualTo("Hello from server!"));
        });
    }

    [Test]
    public async Task Server_response_204_no_content_has_no_body()
    {
        var fixture = _fixture!;
        var clientStream = await fixture.CreateClientRequestStreamAsync();

        await clientStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["GET"],
            [":path"] = ["/"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });
        clientStream.Finish();

        var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);
        await foreach (var _ in serverStream.ReadHeaders(CancellationToken.None))
        {
        }

        await serverStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":status"] = ["204"],
        }, CancellationToken.None);
        serverStream.Finish();

        await foreach (var _ in clientStream.ReadHeaders(CancellationToken.None))
        {
        }

        var buffer = new byte[256];
        var bytesRead = await clientStream.ReadAsync(buffer, CancellationToken.None);

        Assert.That(bytesRead, Is.Zero);
    }

    [Test]
    public async Task Server_response_with_multiple_data_frames_read_by_client()
    {
        var fixture = _fixture!;
        var clientStream = await fixture.CreateClientRequestStreamAsync();
        var chunk1 = "Chunk1"u8.ToArray();
        var chunk2 = "Chunk2"u8.ToArray();
        var chunk3 = "Chunk3"u8.ToArray();

        await clientStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["GET"],
            [":path"] = ["/"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });
        clientStream.Finish();

        var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);
        await foreach (var _ in serverStream.ReadHeaders(CancellationToken.None))
        {
        }

        await serverStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":status"] = ["200"],
        }, CancellationToken.None);
        await serverStream.WriteAsync(chunk1, CancellationToken.None);
        await serverStream.WriteAsync(chunk2, CancellationToken.None);
        await serverStream.WriteAsync(chunk3, CancellationToken.None);
        serverStream.Finish();

        await foreach (var _ in clientStream.ReadHeaders(CancellationToken.None))
        {
        }

        var totalReceived = new List<byte>();
        var buffer = new byte[256];

        while (true)
        {
            var bytesRead = await clientStream.ReadAsync(buffer, CancellationToken.None);
            if (bytesRead == 0)
                break;
            totalReceived.AddRange(buffer[..bytesRead]);
        }

        Assert.That(Encoding.UTF8.GetString([.. totalReceived]), Is.EqualTo("Chunk1Chunk2Chunk3"));
    }

    [Test]
    public async Task Full_request_response_round_trip()
    {
        var fixture = _fixture!;
        var requestBody = "request payload"u8.ToArray();
        var clientStream = await fixture.CreateClientRequestStreamAsync();

        await clientStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["POST"],
            [":path"] = ["/echo"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });
        await clientStream.WriteAsync(requestBody, CancellationToken.None);
        clientStream.Finish();

        var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);
        await foreach (var _ in serverStream.ReadHeaders(CancellationToken.None))
        {
        }

        var receivedBody = new byte[256];
        var receivedBytes = await serverStream.ReadAsync(receivedBody, CancellationToken.None);

        await serverStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":status"] = ["200"],
            ["x-echo"] = ["true"],
        }, CancellationToken.None);

        await serverStream.WriteAsync(receivedBody[..receivedBytes], CancellationToken.None);

        serverStream.Finish();

        await foreach (var _ in clientStream.ReadHeaders(CancellationToken.None))
        {
        }

        var responseBuffer = new byte[256];
        var responseBytes = await clientStream.ReadAsync(responseBuffer, default);

        Assert.That(Encoding.UTF8.GetString(responseBuffer, 0, responseBytes), Is.EqualTo("request payload"));
    }
}