using System.Net.Quic;
using System.Runtime.Versioning;
using System.Text;
using Arbiter.Http3.Tests.Helpers;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3MultipleDataFrameTests
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
    public async Task Client_sends_body_in_multiple_DATA_frames()
    {
        var fixture = _fixture!;
        var chunk1 = "Hello"u8.ToArray();
        var chunk2 = ", "u8.ToArray();
        var chunk3 = "World!"u8.ToArray();

        var requestStream = await fixture.CreateClientRequestStreamAsync();

        await requestStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["POST"],
            [":path"] = ["/upload"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });
        await requestStream.WriteAsync(chunk1);
        await requestStream.WriteAsync(chunk2);
        await requestStream.WriteAsync(chunk3);
        await requestStream.FinishAsync();

        var serverStream = await fixture.AcceptRequestStream(default);
        await foreach (var _ in serverStream.ReadHeaders(default))
        {
        }

        var totalReceived = new List<byte>();
        var buffer = new byte[256];

        while (true)
        {
            var bytesRead = await serverStream.ReadAsync(buffer, default);
            if (bytesRead == 0)
                break;
            totalReceived.AddRange(buffer[..bytesRead]);
        }

        Assert.That(Encoding.UTF8.GetString([.. totalReceived]), Is.EqualTo("Hello, World!"));
    }

    [Test]
    public async Task Empty_DATA_frame_skipped_correctly()
    {
        var fixture = _fixture!;
        var requestStream = await fixture.CreateClientRequestStreamAsync();

        await requestStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["POST"],
            [":path"] = ["/empty"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });
        await requestStream.WriteAsync(Array.Empty<byte>());
        await requestStream.WriteAsync("data"u8.ToArray());
        await requestStream.WriteAsync(Array.Empty<byte>());
        await requestStream.FinishAsync();

        var serverStream = await fixture.AcceptRequestStream(default);
        await foreach (var _ in serverStream.ReadHeaders(default))
        {
        }

        var totalReceived = new List<byte>();
        var buffer = new byte[256];

        while (true)
        {
            var bytesRead = await serverStream.ReadAsync(buffer, default);
            if (bytesRead == 0)
                break;
            totalReceived.AddRange(buffer[..bytesRead]);
        }

        Assert.That(Encoding.UTF8.GetString([.. totalReceived]), Is.EqualTo("data"));
    }

    [Test]
    public async Task Single_byte_DATA_frame()
    {
        var fixture = _fixture!;
        var requestStream = await fixture.CreateClientRequestStreamAsync();

        await requestStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["POST"],
            [":path"] = ["/single-byte"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });
        await requestStream.WriteAsync("A"u8.ToArray());
        await requestStream.WriteAsync("BC"u8.ToArray());
        await requestStream.FinishAsync();

        var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);
        await foreach (var _ in serverStream.ReadHeaders(CancellationToken.None))
        {
        }

        var totalReceived = new List<byte>();
        var buffer = new byte[256];

        while (true)
        {
            var bytesRead = await serverStream.ReadAsync(buffer, CancellationToken.None);
            if (bytesRead == 0)
                break;
            totalReceived.AddRange(buffer[..bytesRead]);
        }

        Assert.That(Encoding.UTF8.GetString([.. totalReceived]), Is.EqualTo("ABC"));
    }

    [Test]
    public async Task Large_body_across_multiple_DATA_frames()
    {
        var fixture = _fixture!;
        var chunkSize = 1024;
        var chunkCount = 5;
        var expectedTotal = chunkSize * chunkCount;

        var requestStream = await fixture.CreateClientRequestStreamAsync();

        await requestStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["POST"],
            [":path"] = ["/large"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });

        for (var i = 0; i < chunkCount; i++)
            await requestStream.WriteAsync(new byte[chunkSize], CancellationToken.None);

        await requestStream.FinishAsync();

        var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);
        await foreach (var _ in serverStream.ReadHeaders(CancellationToken.None))
        {
        }

        var totalReceived = 0;
        var buffer = new byte[2048];

        while (true)
        {
            var bytesRead = await serverStream.ReadAsync(buffer, CancellationToken.None);
            if (bytesRead == 0)
                break;
            totalReceived += bytesRead;
        }

        Assert.That(totalReceived, Is.EqualTo(expectedTotal));
    }
}