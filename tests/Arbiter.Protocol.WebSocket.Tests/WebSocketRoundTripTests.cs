using System.Text;

namespace Arbiter.Protocol.WebSocket.Tests;

[TestFixture]
public class WebSocketRoundTripTests
{
    [Test]
    public async Task Writer_to_reader_text_frame()
    {
        using var ms = new MemoryStream();
        var writer = new WebSocketFrameWriter(ms);
        await writer.WriteText("Hello");
        ms.Position = 0;

        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Text));
            Assert.That(frame.Fin, Is.True);
            Assert.That(Encoding.UTF8.GetString(frame.Payload.Span), Is.EqualTo("Hello"));
        }
    }

    [Test]
    public async Task Writer_to_reader_binary_frame()
    {
        using var ms = new MemoryStream();
        var writer = new WebSocketFrameWriter(ms);
        await writer.WriteBinary(new byte[] {
            0x01, 0x02, 0x03,
        });

        ms.Position = 0;

        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Binary));
            Assert.That(frame.Payload.ToArray(), Is.EqualTo(new byte[] {
                0x01, 0x02, 0x03,
            }));
        }
    }

    [Test]
    public async Task Writer_to_reader_close_frame_with_reason()
    {
        using var ms = new MemoryStream();
        var writer = new WebSocketFrameWriter(ms);
        await writer.WriteClose(WebSocketCloseStatusCode.ProtocolError, "bad frame");
        ms.Position = 0;

        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Close));
            var code = (frame.Payload.Span[0] << 8) | frame.Payload.Span[1];
            Assert.That(code, Is.EqualTo((int)WebSocketCloseStatusCode.ProtocolError));
            var reason = Encoding.UTF8.GetString(frame.Payload.Span[2..]);
            Assert.That(reason, Is.EqualTo("bad frame"));
        }
    }

    [Test]
    public async Task Writer_to_reader_ping_pong_exchange()
    {
        using var ms = new MemoryStream();
        var writer = new WebSocketFrameWriter(ms);
        var pingData = new byte[] {
            0xAA, 0xBB, 0xCC,
        };

        await writer.WritePing(pingData);
        ms.Position = 0;

        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Ping));
            Assert.That(frame.Payload.ToArray(), Is.EqualTo(pingData));
        }

        ms.SetLength(0);
        ms.Position = 0;

        await writer.WritePong(frame.Payload);
        ms.Position = 0;

        var pongFrame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pongFrame.Opcode, Is.EqualTo(WebSocketOpcode.Pong));
            Assert.That(pongFrame.Payload.ToArray(), Is.EqualTo(pingData));
        }
    }

    [Test]
    public async Task Writer_to_reader_fragmented_text_message()
    {
        using var ms = new MemoryStream();
        var writer = new WebSocketFrameWriter(ms);

        await writer.WriteFrame(WebSocketOpcode.Text, false, "Hel"u8.ToArray());
        await writer.WriteFrame(WebSocketOpcode.Continuation, false, "lo "u8.ToArray());
        await writer.WriteFrame(WebSocketOpcode.Continuation, true, "World"u8.ToArray());
        ms.Position = 0;

        var reader = new WebSocketFrameReader(ms);
        var sb = new StringBuilder();
        WebSocketFrame frame;

        frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Text));
            Assert.That(frame.Fin, Is.False);
        }

        sb.Append(Encoding.UTF8.GetString(frame.Payload.Span));

        frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Continuation));
            Assert.That(frame.Fin, Is.False);
        }

        sb.Append(Encoding.UTF8.GetString(frame.Payload.Span));

        frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Continuation));
            Assert.That(frame.Fin, Is.True);
        }

        sb.Append(Encoding.UTF8.GetString(frame.Payload.Span));

        Assert.That(sb.ToString(), Is.EqualTo("Hello World"));
    }

    [Test]
    public async Task Writer_to_reader_multiple_frames_in_sequence()
    {
        using var ms = new MemoryStream();
        var writer = new WebSocketFrameWriter(ms);

        await writer.WriteText("one");
        await writer.WriteBinary("B"u8.ToArray());
        await writer.WriteText("two");
        ms.Position = 0;

        var reader = new WebSocketFrameReader(ms);
        var frame1 = await reader.ReadFrame();
        var frame2 = await reader.ReadFrame();
        var frame3 = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame1.Opcode, Is.EqualTo(WebSocketOpcode.Text));
            Assert.That(Encoding.UTF8.GetString(frame1.Payload.Span), Is.EqualTo("one"));
            Assert.That(frame2.Opcode, Is.EqualTo(WebSocketOpcode.Binary));
            Assert.That(frame2.Payload.Span[0], Is.EqualTo(0x42));
            Assert.That(frame3.Opcode, Is.EqualTo(WebSocketOpcode.Text));
            Assert.That(Encoding.UTF8.GetString(frame3.Payload.Span), Is.EqualTo("two"));
        }
    }

    [Test]
    public async Task Writer_to_reader_large_binary_payload()
    {
        var payload = new byte[100_000];
        var random = new Random(42);
        random.NextBytes(payload);

        using var ms = new MemoryStream();
        var writer = new WebSocketFrameWriter(ms);
        await writer.WriteBinary(payload);
        ms.Position = 0;

        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        Assert.That(frame.Payload.ToArray(), Is.EqualTo(payload));
    }

    [Test]
    public async Task Writer_to_reader_empty_close()
    {
        using var ms = new MemoryStream();
        var writer = new WebSocketFrameWriter(ms);
        await writer.WriteClose();
        ms.Position = 0;

        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Close));
            Assert.That(frame.Payload.Length, Is.EqualTo(2));
        }
    }

    [TestCase(125)]
    [TestCase(126)]
    [TestCase(127)]
    [TestCase(65535)]
    [TestCase(65536)]
    public async Task Writer_to_reader_payload_length_boundary(int size)
    {
        var payload = new byte[size];

        for (var i = 0; i < size; i++)
            payload[i] = (byte)(i & 0xFF);

        using var ms = new MemoryStream();
        var writer = new WebSocketFrameWriter(ms);
        await writer.WriteBinary(payload);
        ms.Position = 0;

        var reader = new WebSocketFrameReader(ms);
        var frame = await reader.ReadFrame();

        Assert.That(frame.Payload.Length, Is.EqualTo(size));
        Assert.That(frame.Payload.ToArray(), Is.EqualTo(payload));
    }
}
