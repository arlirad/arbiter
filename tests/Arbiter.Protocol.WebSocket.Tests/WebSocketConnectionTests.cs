using System.IO.Pipelines;

namespace Arbiter.Protocol.WebSocket.Tests;

[TestFixture]
public class WebSocketConnectionTests
{
    private static (WebSocketConnection client, WebSocketConnection server) CreatePair()
    {
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
        var reversePipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        var clientStream = new DuplexPipeStream(pipe.Reader, reversePipe.Writer);
        var serverStream = new DuplexPipeStream(reversePipe.Reader, pipe.Writer);

        var client = new WebSocketConnection(clientStream);
        var server = new WebSocketConnection(serverStream);

        return (client, server);
    }

    [Test]
    public async Task SendText_receive_text()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            await client.SendTextAsync("Hello, World!");
            var received = await server.ReceiveTextAsync();

            Assert.That(received, Is.EqualTo("Hello, World!"));
        }
    }

    [Test]
    public async Task SendBinary_receive_binary()
    {
        var (client, server) = CreatePair();

        var data = new byte[] {
            0x01, 0x02, 0x03, 0x04,
        };

        await using (client)
        await using (server)
        {
            await client.SendBinaryAsync(data);
            var received = await server.ReceiveBinaryAsync();

            Assert.That(received.ToArray(), Is.EqualTo(data));
        }
    }

    [Test]
    public async Task Bidirectional_send()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            await client.SendTextAsync("from client");
            await server.SendTextAsync("from server");

            var clientReceived = await client.ReceiveTextAsync();
            var serverReceived = await server.ReceiveTextAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(clientReceived, Is.EqualTo("from server"));
                Assert.That(serverReceived, Is.EqualTo("from client"));
            }
        }
    }

    [Test]
    public async Task Multiple_messages_sequential()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            for (var i = 0; i < 5; i++)
                await client.SendTextAsync($"msg{i}");

            for (var i = 0; i < 5; i++)
            {
                var received = await server.ReceiveTextAsync();
                Assert.That(received, Is.EqualTo($"msg{i}"));
            }
        }
    }

    [Test]
    public async Task Ping_is_auto_ponged()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            await client.SendTextAsync("before-ping");
            var received = await server.ReceiveTextAsync();
            Assert.That(received, Is.EqualTo("before-ping"));
        }
    }

    [Test]
    public async Task Close_sends_close_frame()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            await client.CloseAsync(WebSocketCloseStatusCode.Normal, "done");

            var msg = await server.ReceiveAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(msg.Opcode, Is.EqualTo(WebSocketOpcode.Close));
                Assert.That(msg.AsText(), Is.Null);
            }
        }
    }

    [Test]
    public async Task ReceiveAsync_returns_text_message()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            await client.SendTextAsync("hello");
            var msg = await server.ReceiveAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(msg.Opcode, Is.EqualTo(WebSocketOpcode.Text));
                Assert.That(msg.AsText(), Is.EqualTo("hello"));
            }
        }
    }

    [Test]
    public async Task ReceiveAsync_returns_binary_message()
    {
        var (client, server) = CreatePair();

        var data = new byte[] {
            0xCA, 0xFE,
        };

        await using (client)
        await using (server)
        {
            await client.SendBinaryAsync(data);
            var msg = await server.ReceiveAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(msg.Opcode, Is.EqualTo(WebSocketOpcode.Binary));
                Assert.That(msg.Payload.ToArray(), Is.EqualTo(data));
            }
        }
    }

    [Test]
    public async Task Empty_text_message()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            await client.SendTextAsync("");
            var received = await server.ReceiveTextAsync();

            Assert.That(received, Is.EqualTo(""));
        }
    }

    [Test]
    public async Task Large_text_message()
    {
        var (client, server) = CreatePair();
        var text = new string('A', 100_000);

        await using (client)
        await using (server)
        {
            await client.SendTextAsync(text);
            var received = await server.ReceiveTextAsync();

            Assert.That(received, Is.EqualTo(text));
        }
    }

    [Test]
    public async Task Close_is_idempotent()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            await client.CloseAsync();
            await client.CloseAsync();
            await client.CloseAsync();

            var msg = await server.ReceiveAsync();
            Assert.That(msg.Opcode, Is.EqualTo(WebSocketOpcode.Close));
        }
    }

    [Test]
    public async Task ReceiveText_returns_null_on_close()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            await client.CloseAsync();
            var result = await server.ReceiveTextAsync();

            Assert.That(result, Is.Null);
        }
    }

    [Test]
    public async Task ReceiveBinary_returns_empty_on_close()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            await client.CloseAsync();
            var result = await server.ReceiveBinaryAsync();

            Assert.That(result.Length, Is.Zero);
        }
    }

    [Test]
    public async Task ReceiveAsync_returns_close_message()
    {
        var (client, server) = CreatePair();

        await using (client)
        await using (server)
        {
            await client.CloseAsync(WebSocketCloseStatusCode.GoingAway, "shutdown");
            var msg = await server.ReceiveAsync();

            Assert.That(msg.Opcode, Is.EqualTo(WebSocketOpcode.Close));
        }
    }

    [Test]
    public async Task Utf8_multibyte_roundtrip()
    {
        var (client, server) = CreatePair();
        var text = "\u00E4\u00F6\u00FC\u4E16\u754C\U0001F600";

        await using (client)
        await using (server)
        {
            await client.SendTextAsync(text);
            var received = await server.ReceiveTextAsync();

            Assert.That(received, Is.EqualTo(text));
        }
    }

    private sealed class DuplexPipeStream(PipeReader reader, PipeWriter writer) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var result = await reader.ReadAsync(ct);

            if (result.IsCanceled || result.IsCompleted)
                return 0;

            var available = result.Buffer;
            var toCopy = (int)Math.Min(buffer.Length, available.Length);
            var slice = available.Slice(0, toCopy);

            foreach (var segment in slice)
            {
                segment.Span.CopyTo(buffer.Span[..segment.Length]);
                buffer = buffer[segment.Length..];
            }

            reader.AdvanceTo(available.GetPosition(toCopy));

            return toCopy;
        }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            buffer.CopyTo(writer.GetMemory(buffer.Length));
            writer.Advance(buffer.Length);

            return ValueTask.CompletedTask;
        }

        public override void Write(byte[] buffer, int offset, int count)
            => WriteAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult();

        public async override Task FlushAsync(CancellationToken ct = default) => await writer.FlushAsync(ct);
        public override void Flush() => FlushAsync().GetAwaiter().GetResult();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
