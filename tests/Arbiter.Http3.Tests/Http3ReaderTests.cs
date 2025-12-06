using Arlirad.Http3.Streams;

namespace Arbiter.Http3.Tests;

public class Http3ReaderTests
{
    [TestCase(new byte[] { 0x00 }, 0)]
    [TestCase(new byte[] { 0x25 }, 37)]
    [TestCase(new byte[] { 0x3F }, 63)]
    public async Task ReadVarInt_SingleByte_ReturnsCorrectValue(byte[] data, long expected)
    {
        // Arrange
        using var stream = new MemoryStream(data);
        var reader = new Http3Reader(stream);
        var buffer = new byte[8];

        // Act
        var result = await reader.ReadVarInt(buffer, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(new byte[] { 0x40, 0x00 }, 0)]
    [TestCase(new byte[] { 0x7B, 0xBD }, 15293)]
    [TestCase(new byte[] { 0x7F, 0xFF }, 16383)]
    public async Task ReadVarInt_TwoBytes_ReturnsCorrectValue(byte[] data, long expected)
    {
        // Arrange
        using var stream = new MemoryStream(data);
        var reader = new Http3Reader(stream);
        var buffer = new byte[8];

        // Act
        var result = await reader.ReadVarInt(buffer, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(new byte[] { 0x80, 0x00, 0x00, 0x00 }, 0)]
    [TestCase(new byte[] { 0x9D, 0x7F, 0x3E, 0x7D }, 494878333)]
    [TestCase(new byte[] { 0xBF, 0xFF, 0xFF, 0xFF }, 1073741823)]
    public async Task ReadVarInt_FourBytes_ReturnsCorrectValue(byte[] data, long expected)
    {
        // Arrange
        using var stream = new MemoryStream(data);
        var reader = new Http3Reader(stream);
        var buffer = new byte[8];

        // Act
        var result = await reader.ReadVarInt(buffer, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Theory]
    [TestCase(new byte[] { 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0)]
    [TestCase(new byte[] { 0xC2, 0x19, 0x7C, 0x5E, 0xFF, 0x14, 0xE8, 0x8C }, 151288809941952652)]
    [TestCase(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 4611686018427387903)]
    public async Task ReadVarInt_EightBytes_ReturnsCorrectValue(byte[] data, long expected)
    {
        // Arrange
        using var stream = new MemoryStream(data);
        var reader = new Http3Reader(stream);
        var buffer = new byte[8];

        // Act
        var result = await reader.ReadVarInt(buffer, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ReadVarInt_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        using var stream = new MemoryStream([0xFF, 0xFF]);
        var reader = new Http3Reader(stream);
        var buffer = new byte[8];
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(async () => await reader.ReadVarInt(buffer, cts.Token));
    }

    [Test]
    public void ReadVarInt_StreamTooShort_ThrowsEndOfStreamException()
    {
        // Arrange
        using var stream = new MemoryStream([0x40]); // Indicates 2 bytes but only 1 provided
        var reader = new Http3Reader(stream);
        var buffer = new byte[8];

        // Act & Assert
        Assert.ThrowsAsync<EndOfStreamException>(async () => await reader.ReadVarInt(buffer, CancellationToken.None));
    }
}