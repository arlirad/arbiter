using Arlirad.WebSocket;

namespace Arbiter.Protocol.WebSocket.Tests;

[TestFixture]
public class WebSocketFrameWriterTests
{
    private static async Task<byte[]> WriteAndGetBytes(Func<WebSocketFrameWriter, Task> writeAction)
    {
        using var ms = new MemoryStream();
        var writer = new WebSocketFrameWriter(ms);
        await writeAction(writer);
        return ms.ToArray();
    }

    [Test]
    public async Task WriteFrame_text_small_payload()
    {
        var bytes = await WriteAndGetBytes(w => w.WriteText("Hi"));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Text));
            Assert.That(frame.Fin, Is.True);
            Assert.That(System.Text.Encoding.UTF8.GetString(frame.Payload.Span), Is.EqualTo("Hi"));
        }
    }

    [Test]
    public async Task WriteFrame_binary_payload()
    {
        var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var bytes = await WriteAndGetBytes(w => w.WriteBinary(data));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Binary));
            Assert.That(frame.Payload.ToArray(), Is.EqualTo(data));
        }
    }

    [Test]
    public async Task WriteFrame_ping_with_data()
    {
        var data = new byte[] { 0x01, 0x02 };
        var bytes = await WriteAndGetBytes(w => w.WritePing(data));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Ping));
            Assert.That(frame.Payload.ToArray(), Is.EqualTo(data));
        }
    }

    [Test]
    public async Task WriteFrame_pong()
    {
        var data = new byte[] { 0x03, 0x04 };
        var bytes = await WriteAndGetBytes(w => w.WritePong(data));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Pong));
            Assert.That(frame.Payload.ToArray(), Is.EqualTo(data));
        }
    }

    [Test]
    public async Task WriteClose_with_code_only()
    {
        var bytes = await WriteAndGetBytes(w => w.WriteClose(WebSocketCloseStatusCode.Normal));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Close));
            Assert.That(frame.Payload.Length, Is.EqualTo(2));
            var code = (frame.Payload.Span[0] << 8) | frame.Payload.Span[1];
            Assert.That(code, Is.EqualTo(1000));
        }
    }

    [Test]
    public async Task WriteClose_with_code_and_reason()
    {
        var bytes = await WriteAndGetBytes(w => w.WriteClose(WebSocketCloseStatusCode.GoingAway, "bye"));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Close));
            var code = (frame.Payload.Span[0] << 8) | frame.Payload.Span[1];
            Assert.That(code, Is.EqualTo(1001));
            var reason = System.Text.Encoding.UTF8.GetString(frame.Payload.Span[2..]);
            Assert.That(reason, Is.EqualTo("bye"));
        }
    }

    [Test]
    public async Task WriteFrame_empty_payload()
    {
        var bytes = await WriteAndGetBytes(w => w.WriteFrame(WebSocketOpcode.Ping, true, ReadOnlyMemory<byte>.Empty));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Ping));
            Assert.That(frame.Payload.Length, Is.Zero);
        }
    }

    [Test]
    public async Task WriteFrame_16bit_length_encoding()
    {
        var payload = new byte[256];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i & 0xFF);

        var bytes = await WriteAndGetBytes(w => w.WriteBinary(payload));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        Assert.That(frame.Payload.ToArray(), Is.EqualTo(payload));
    }

    [Test]
    public async Task WriteFrame_64bit_length_encoding()
    {
        var payload = new byte[70000];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i & 0xFF);

        var bytes = await WriteAndGetBytes(w => w.WriteBinary(payload));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        Assert.That(frame.Payload.ToArray(), Is.EqualTo(payload));
    }

    [Test]
    public async Task WriteFrame_fin_false()
    {
        var bytes = await WriteAndGetBytes(w => w.WriteFrame(WebSocketOpcode.Text, false, "part"u8.ToArray()));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        Assert.That(frame.Fin, Is.False);
    }

    [Test]
    public async Task WriteFrame_server_frames_are_unmasked()
    {
        var bytes = await WriteAndGetBytes(w => w.WriteText("test"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bytes[1] & 0x80, Is.Zero, "Mask bit should be 0 for server frames");
        }
    }

    [Test]
    public async Task WriteFrame_length_boundary_125()
    {
        var payload = new byte[125];
        var bytes = await WriteAndGetBytes(w => w.WriteBinary(payload));

        Assert.That(bytes[1] & 0x7F, Is.EqualTo(125));
    }

    [Test]
    public async Task WriteFrame_length_boundary_126()
    {
        var payload = new byte[126];
        var bytes = await WriteAndGetBytes(w => w.WriteBinary(payload));

        Assert.That(bytes[1] & 0x7F, Is.EqualTo(126));
    }

    [Test]
    public async Task WriteFrame_length_boundary_65535()
    {
        var payload = new byte[65535];
        var bytes = await WriteAndGetBytes(w => w.WriteBinary(payload));

        Assert.That(bytes[1] & 0x7F, Is.EqualTo(126));
    }

    [Test]
    public async Task WriteFrame_length_boundary_65536()
    {
        var payload = new byte[65536];
        var bytes = await WriteAndGetBytes(w => w.WriteBinary(payload));

        Assert.That(bytes[1] & 0x7F, Is.EqualTo(127));
    }

    [Test]
    public async Task WriteText_utf8_roundtrip()
    {
        var text = "Hello, \u4E16\u754C! \U0001F600";
        var bytes = await WriteAndGetBytes(w => w.WriteText(text));
        using var ms = new MemoryStream(bytes);
        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        Assert.That(System.Text.Encoding.UTF8.GetString(frame.Payload.Span), Is.EqualTo(text));
    }
}
