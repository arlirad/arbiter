using Arlirad.Http3.Streams;

namespace Arbiter.Http3.Tests;

[TestFixture]
public class ClampedStreamTests
{
    [Test]
    public void Read_DoesNotExceedSpecifiedLength()
    {
        // Arrange
        var data = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        using var inner = new MemoryStream(data);
        using var clamped = new ClampedStream(inner, length: 10);

        var buffer = new byte[20];

        // Act
        var read = clamped.Read(buffer, 0, buffer.Length);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(read, Is.EqualTo(10));
            Assert.That(buffer.Take(10).ToArray(), Is.EqualTo(data.Take(10).ToArray()));
            Assert.That(buffer.Skip(10).Take(10).ToArray(), Is.EqualTo(new byte[10]));
        });
    }

    [Test]
    public void Read_MultipleCalls_StopAtLength()
    {
        // Arrange
        var data = Enumerable.Range(0, 50).Select(i => (byte)i).ToArray();
        using var inner = new MemoryStream(data);
        using var clamped = new ClampedStream(inner, length: 15);

        var b1 = new byte[7];
        var b2 = new byte[7];
        var b3 = new byte[7];

        // Act
        var r1 = clamped.Read(b1, 0, b1.Length); // 7
        var r2 = clamped.Read(b2, 0, b2.Length); // 7
        var r3 = clamped.Read(b3, 0, b3.Length); // 1 (remaining)
        var r4 = clamped.Read(b3, 0, b3.Length); // 0 (exhausted)

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(r1, Is.EqualTo(7));
            Assert.That(r2, Is.EqualTo(7));
            Assert.That(r3, Is.EqualTo(1));
            Assert.That(r4, Is.EqualTo(0));

            Assert.That(b1, Is.EqualTo(data.Take(7).ToArray()));
            Assert.That(b2, Is.EqualTo(data.Skip(7).Take(7).ToArray()));
            Assert.That(b3, Is.EqualTo(data.Skip(14).Take(1).Concat(new byte[6]).ToArray()));
        });
    }

    [Test]
    public async Task ReadAsync_RespectsClamp()
    {
        // Arrange
        var data = Enumerable.Range(0, 30).Select(i => (byte)i).ToArray();
        using var inner = new MemoryStream(data);
        await inner.FlushAsync();
        using var clamped = new ClampedStream(inner, length: 12);

        var buffer = new byte[100];

        // Act
        var r1 = await clamped.ReadAsync(buffer, CancellationToken.None);
        var r2 = await clamped.ReadAsync(buffer, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(r1, Is.EqualTo(12));
            Assert.That(r2, Is.EqualTo(0));
            Assert.That(buffer.Take(12).ToArray(), Is.EqualTo(data.Take(12).ToArray()));
        });
    }

    [Test]
    public void Write_IsNotSupported()
    {
        using var inner = new MemoryStream(new byte[10]);
        using var clamped = new ClampedStream(inner, 5);

        Assert.Throws<NotSupportedException>(() => clamped.Write(Array.Empty<byte>(), 0, 0));
        Assert.ThrowsAsync<NotSupportedException>(async () => await clamped.WriteAsync(ReadOnlyMemory<byte>.Empty));
        Assert.Throws<NotSupportedException>(() => clamped.SetLength(0));
        Assert.Throws<NotSupportedException>(() => clamped.Seek(0, SeekOrigin.Begin));
    }

    [Test]
    public void Position_ReflectsInner()
    {
        var data = Enumerable.Range(0, 10).Select(i => (byte)i).ToArray();
        using var inner = new MemoryStream(data);
        using var clamped = new ClampedStream(inner, 5);

        var buffer = new byte[3];
        var r1 = clamped.Read(buffer, 0, buffer.Length);
        Assert.Multiple(() =>
        {
            Assert.That(r1, Is.EqualTo(3));
            Assert.That(clamped.Position, Is.EqualTo(inner.Position));
        });

        var r2 = clamped.Read(buffer, 0, buffer.Length);
        Assert.Multiple(() =>
        {
            Assert.That(r2, Is.EqualTo(2));
            Assert.That(clamped.Position, Is.EqualTo(inner.Position));
        });

        var r3 = clamped.Read(buffer, 0, buffer.Length);
        Assert.That(r3, Is.EqualTo(0));
    }
}