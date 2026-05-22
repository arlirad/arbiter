using System.Text;

namespace Arbiter.Protocol.WebSocket.Tests;

[TestFixture]
public class WebSocketFrameReaderTests
{
    private static byte[] BuildFrame(WebSocketOpcode opcode, bool fin, byte[] payload, bool masked, byte[]? mask = null)
    {
        using var ms = new MemoryStream();
        var firstByte = (byte)((fin ? 0x80 : 0x00) | ((int)opcode & 0x0F));
        ms.WriteByte(firstByte);

        var maskBit = masked ? 0x80 : 0x00;

        if (payload.Length <= 125)
        {
            ms.WriteByte((byte)(maskBit | payload.Length));
        }
        else if (payload.Length <= 65535)
        {
            ms.WriteByte((byte)(maskBit | 126));
            ms.WriteByte((byte)((payload.Length >> 8) & 0xFF));
            ms.WriteByte((byte)(payload.Length & 0xFF));
        }
        else
        {
            ms.WriteByte((byte)(maskBit | 127));
            var len = (ulong)payload.Length;

            for (var i = 0; i < 8; i++)
                ms.WriteByte((byte)((len >> (56 - (i * 8))) & 0xFF));
        }

        if (masked)
        {
            var actualMask = mask ?? [0x37, 0xFA, 0x21, 0x3D];
            ms.Write(actualMask);

            var maskedPayload = new byte[payload.Length];

            for (var i = 0; i < payload.Length; i++)
                maskedPayload[i] = (byte)(payload[i] ^ actualMask[i % 4]);

            ms.Write(maskedPayload);
        }
        else
        {
            ms.Write(payload);
        }

        return ms.ToArray();
    }

    [Test]
    public async Task ReadFrame_small_text_unmasked()
    {
        var payload = "Hello"u8.ToArray();
        var data = BuildFrame(WebSocketOpcode.Text, true, payload, false);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Text));
            Assert.That(frame.Fin, Is.True);
            Assert.That(frame.Payload.ToArray(), Is.EqualTo(payload));
        }
    }

    [Test]
    public async Task ReadFrame_binary_unmasked()
    {
        var payload = new byte[] {
            0x01, 0x02, 0x03, 0x04,
        };

        var data = BuildFrame(WebSocketOpcode.Binary, true, payload, false);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Binary));
            Assert.That(frame.Fin, Is.True);
            Assert.That(frame.Payload.ToArray(), Is.EqualTo(payload));
        }
    }

    [Test]
    public async Task ReadFrame_masked_text_unmasks_payload()
    {
        var payload = "Hello"u8.ToArray();

        var mask = new byte[] {
            0x37, 0xFA, 0x21, 0x3D,
        };

        var data = BuildFrame(WebSocketOpcode.Text, true, payload, true, mask);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        Assert.That(Encoding.UTF8.GetString(frame.Payload.Span), Is.EqualTo("Hello"));
    }

    [Test]
    public async Task ReadFrame_fragmented_fin_false()
    {
        var payload = "part"u8.ToArray();
        var data = BuildFrame(WebSocketOpcode.Text, false, payload, false);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        Assert.That(frame.Fin, Is.False);
    }

    [Test]
    public async Task ReadFrame_ping_with_payload()
    {
        var payload = new byte[] {
            0xAB, 0xCD,
        };

        var data = BuildFrame(WebSocketOpcode.Ping, true, payload, false);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Ping));
            Assert.That(frame.Payload.ToArray(), Is.EqualTo(payload));
        }
    }

    [Test]
    public async Task ReadFrame_close_with_status_code()
    {
        var payload = new byte[] {
            0x03, 0xE8,
        };

        var data = BuildFrame(WebSocketOpcode.Close, true, payload, false);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Close));
            var code = (frame.Payload.Span[0] << 8) | frame.Payload.Span[1];
            Assert.That(code, Is.EqualTo(1000));
        }
    }

    [Test]
    public async Task ReadFrame_16bit_payload_length()
    {
        var payload = new byte[256];

        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i & 0xFF);

        var data = BuildFrame(WebSocketOpcode.Binary, true, payload, false);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        Assert.That(frame.Payload.Length, Is.EqualTo(256));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(frame.Payload.Span[0], Is.Zero);
            Assert.That(frame.Payload.Span[255], Is.EqualTo(255));
        }
    }

    [Test]
    public async Task ReadFrame_64bit_payload_length()
    {
        var payload = new byte[70000];

        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i & 0xFF);

        var data = BuildFrame(WebSocketOpcode.Binary, true, payload, false);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        Assert.That(frame.Payload.Length, Is.EqualTo(70000));
    }

    [Test]
    public async Task ReadFrame_empty_payload()
    {
        var data = BuildFrame(WebSocketOpcode.Ping, true, [], false);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        Assert.That(frame.Payload.Length, Is.Zero);
    }

    [Test]
    public async Task ReadFrame_continuation_opcode()
    {
        var payload = "more"u8.ToArray();
        var data = BuildFrame(WebSocketOpcode.Continuation, true, payload, false);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Continuation));
    }

    [Test]
    public async Task ReadFrame_pong_opcode()
    {
        var data = BuildFrame(WebSocketOpcode.Pong, true, [], false);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        Assert.That(frame.Opcode, Is.EqualTo(WebSocketOpcode.Pong));
    }

    [Test]
    public void ReadFrame_throws_on_truncated_stream()
    {
        var data = new byte[] {
            0x81, 0x05, 0x48,
        };

        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        Assert.ThrowsAsync<EndOfStreamException>(async () => await reader.ReadFrame());
    }

    [Test]
    public async Task ReadFrame_masked_empty_payload()
    {
        var mask = new byte[] {
            0xAA, 0xBB, 0xCC, 0xDD,
        };

        var data = BuildFrame(WebSocketOpcode.Text, true, [], true, mask);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        Assert.That(frame.Payload.Length, Is.Zero);
    }

    [Test]
    public async Task ReadFrame_multiple_frames_sequentially()
    {
        var frame1 = BuildFrame(WebSocketOpcode.Text, true, "first"u8.ToArray(), false);
        var frame2 = BuildFrame(WebSocketOpcode.Binary, true, [0x01, 0x02], false);
        var combined = new byte[frame1.Length + frame2.Length];
        frame1.CopyTo(combined, 0);
        frame2.CopyTo(combined, frame1.Length);

        using var stream = new MemoryStream(combined);
        var reader = new WebSocketFrameReader(stream);

        var read1 = await reader.ReadFrame();
        var read2 = await reader.ReadFrame();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(read1.Opcode, Is.EqualTo(WebSocketOpcode.Text));
            Assert.That(read2.Opcode, Is.EqualTo(WebSocketOpcode.Binary));
            Assert.That(Encoding.UTF8.GetString(read1.Payload.Span), Is.EqualTo("first"));

            Assert.That(read2.Payload.ToArray(), Is.EqualTo(new byte[] {
                0x01, 0x02,
            }));
        }
    }

    [Test]
    public async Task ReadFrame_masked_16bit_payload()
    {
        var payload = new byte[200];

        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i & 0xFF);

        var mask = new byte[] {
            0x11, 0x22, 0x33, 0x44,
        };

        var data = BuildFrame(WebSocketOpcode.Binary, true, payload, true, mask);
        using var stream = new MemoryStream(data);
        var reader = new WebSocketFrameReader(stream);

        var frame = await reader.ReadFrame();

        Assert.That(frame.Payload.ToArray(), Is.EqualTo(payload));
    }
}
