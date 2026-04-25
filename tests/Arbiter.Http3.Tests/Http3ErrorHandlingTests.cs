using System.Collections.Generic;
using System.Net.Quic;
using System.Runtime.Versioning;
using Arbiter.Http3.Tests.Helpers;
using Arlirad.Http3.Streams;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3ErrorHandlingTests
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
    public async Task Headers_only_stream_ends_gracefully()
    {
        var fixture = _fixture!;
        var requestStream = await fixture.CreateClientRequestStreamAsync();

        await requestStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["GET"],
            [":path"] = ["/"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        }, CancellationToken.None);

        requestStream.Finish();

        var serverStream = await fixture.AcceptRequestStream(default);

        var headerCount = 0;
        await foreach (var _ in serverStream.ReadHeaders(default))
            headerCount++;

        Assert.That(headerCount, Is.GreaterThan(0));

        var buffer = new byte[100];
        var bytesRead = await serverStream.ReadAsync(buffer, default);

        Assert.That(bytesRead, Is.EqualTo(0));
    }

    [Test]
    public async Task Server_reads_headers_without_all_pseudo_headers()
    {
        var fixture = _fixture!;
        var requestStream = await fixture.CreateClientRequestStreamAsync();

        await requestStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":scheme"] = ["https"],
            ["x-custom"] = ["test"],
        }, CancellationToken.None);

        requestStream.Finish();

        var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);

        var headers = new List<KeyValuePair<string, string?>>();
        await foreach (var header in serverStream.ReadHeaders(CancellationToken.None))
            headers.Add(header);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(headers, Has.Count.EqualTo(2));
            Assert.That(headers.Any(h => h is { Key: ":scheme", Value: "https" }));
            Assert.That(headers.Any(h => h is { Key: "x-custom", Value: "test" }));
        }
    }
}