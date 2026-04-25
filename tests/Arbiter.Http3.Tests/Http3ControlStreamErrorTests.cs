using System.Net.Quic;
using System.Runtime.Versioning;
using Arbiter.Http3.Tests.Helpers;
using Arlirad.Http3.Streams;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3ControlStreamErrorTests
{
    private RawQuicFixture? _fixture;

    [SetUp]
    public async Task SetUp()
    {
        if (!QuicListener.IsSupported)
            Assert.Ignore("QUIC is not supported on this platform");

        _fixture = await RawQuicFixture.CreateAsync();
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
            catch
            {
            }

            _fixture = null;
        }
    }

    private static async Task WriteVarIntToStream(Stream stream, ulong value, CancellationToken ct)
    {
        var writer = new Http3Writer(stream);
        var buffer = new byte[16];
        await writer.WriteVarInt(value, buffer, ct);
    }

    private static async Task<QuicStream> OpenClientControlStreamWithSettings(RawQuicFixture fixture, CancellationToken ct)
    {
        var stream = await fixture.OpenClientUnidirectionalStreamAsync();
        await WriteVarIntToStream(stream, 0x00, ct);

        using var payload = new MemoryStream();
        var payloadWriter = new Http3Writer(payload);
        var buffer = new byte[16];
        await payloadWriter.WriteVarInt(0x04, buffer, ct);
        await payloadWriter.WriteVarInt(0, buffer, ct);
        payload.Position = 0;

        await WriteVarIntToStream(stream, 0x04, ct);
        await WriteVarIntToStream(stream, (ulong)payload.Length, ct);
        await payload.CopyToAsync(stream, ct);

        return stream;
    }

    private static async Task AssertConnectionClosedByServer(RawQuicFixture fixture)
    {
        await Task.Delay(500);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await fixture.ClientQuicConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cts.Token);
            Assert.Fail("Client should not be able to open streams after server closed connection");
        }
        catch (QuicException)
        {
            Assert.Pass();
        }
    }

    [Test]
    public async Task Duplicate_control_stream_closes_connection()
    {
        var fixture = _fixture!;
        var server = fixture.CreateServerHttp3Connection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        _ = server.Start();

        await OpenClientControlStreamWithSettings(fixture, cts.Token);
        await Task.Delay(200);
        await OpenClientControlStreamWithSettings(fixture, cts.Token);

        await AssertConnectionClosedByServer(fixture);
    }

    [Test]
    public async Task Push_stream_from_client_closes_connection()
    {
        var fixture = _fixture!;
        var server = fixture.CreateServerHttp3Connection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        _ = server.Start();
        await Task.Delay(200);

        var stream = await fixture.OpenClientUnidirectionalStreamAsync();
        await WriteVarIntToStream(stream, 0x01, cts.Token);

        await AssertConnectionClosedByServer(fixture);
    }

    [Test]
    public async Task Non_SETTINGS_first_frame_on_control_stream_closes_connection()
    {
        var fixture = _fixture!;
        var server = fixture.CreateServerHttp3Connection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        _ = server.Start();
        await Task.Delay(200);

        var stream = await fixture.OpenClientUnidirectionalStreamAsync();
        await WriteVarIntToStream(stream, 0x00, cts.Token);
        await WriteVarIntToStream(stream, 0x00, cts.Token);
        await WriteVarIntToStream(stream, 0, cts.Token);

        await AssertConnectionClosedByServer(fixture);
    }

    [Test]
    public async Task DATA_frame_on_control_stream_closes_connection()
    {
        var fixture = _fixture!;
        var server = fixture.CreateServerHttp3Connection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        _ = server.Start();
        await Task.Delay(200);

        var controlStream = await OpenClientControlStreamWithSettings(fixture, cts.Token);
        await Task.Delay(200);

        await WriteVarIntToStream(controlStream, 0x00, cts.Token);
        await WriteVarIntToStream(controlStream, 0, cts.Token);

        await AssertConnectionClosedByServer(fixture);
    }

    [Test]
    public async Task Unknown_stream_type_accepted_without_error()
    {
        var fixture = _fixture!;
        var server = fixture.CreateServerHttp3Connection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        _ = server.Start();
        await Task.Delay(200);

        var stream = await fixture.OpenClientUnidirectionalStreamAsync();
        await WriteVarIntToStream(stream, 0x21, cts.Token);

        await Task.Delay(300);

        using var verifyCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var verifyStream = await fixture.ClientQuicConnection.OpenOutboundStreamAsync(
            QuicStreamType.Bidirectional, verifyCts.Token);
        Assert.That(verifyStream, Is.Not.Null);
    }
}