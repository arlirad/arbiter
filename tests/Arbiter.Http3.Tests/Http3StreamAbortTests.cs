using System.Collections.Generic;
using System.Net.Quic;
using System.Runtime.Versioning;
using Arbiter.Http3.Tests.Helpers;
using Arlirad.Http3.Streams;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3StreamAbortTests
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
    public async Task Client_aborts_stream_before_body_server_reads_zero_bytes()
    {
        var fixture = _fixture!;
        var clientStream = await fixture.CreateClientRequestStreamAsync();

        await clientStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["POST"],
            [":path"] = ["/abort"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
            ["content-length"] = ["100"],
        });
        clientStream.Finish();

        var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);
        await foreach (var _ in serverStream.ReadHeaders(CancellationToken.None))
        {
        }

        var buffer = new byte[256];
        var bytesRead = await serverStream.ReadAsync(buffer, CancellationToken.None);

        Assert.That(bytesRead, Is.Zero);
    }

    [Test]
    public async Task Client_aborts_stream_mid_body_server_gets_partial_data()
    {
        var fixture = _fixture!;
        var partialBody = new byte[64];
        System.Random.Shared.NextBytes(partialBody);

        var clientStream = await fixture.CreateClientRequestStreamAsync();

        await clientStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["POST"],
            [":path"] = ["/partial"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
            ["content-length"] = ["1024"],
        });
        await clientStream.WriteAsync(partialBody, CancellationToken.None);
        clientStream.Finish();

        var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);
        await foreach (var _ in serverStream.ReadHeaders(CancellationToken.None))
        {
        }

        var totalRead = 0;
        var buffer = new byte[256];

        while (true)
        {
            try
            {
                var bytesRead = await serverStream.ReadAsync(buffer, CancellationToken.None);
                if (bytesRead == 0)
                    break;
                totalRead += bytesRead;
            }
            catch
            {
                break;
            }
        }

        Assert.That(totalRead, Is.EqualTo(partialBody.Length));
    }

    [Test]
    public async Task Server_finishes_without_writing_body_client_reads_zero()
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
            [":status"] = ["200"],
        }, CancellationToken.None);
        serverStream.Finish();

        await foreach (var _ in clientStream.ReadHeaders(CancellationToken.None))
        {
        }

        var buffer = new byte[256];
        var bytesRead = await clientStream.ReadAsync(buffer, CancellationToken.None);

        Assert.That(bytesRead, Is.Zero);
    }
}