using System.Net.Quic;
using Arlirad.Http3.Streams;

namespace Arbiter.Protocol.Http3.Tests;

public class Http3WriterTests
{
    [Theory]
    [TestCase(0ul, new byte[] { 0x00 })]
    [TestCase(37ul, new byte[] { 0x25 })]
    [TestCase(63ul, new byte[] { 0x3F })]
    public async Task WriteVarInt_SingleByte_WritesCorrectValue(ulong value, byte[] expected)
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new Http3Writer(stream);
        var buffer = new byte[8];

        // Act
        await writer.WriteVarInt(value, buffer, CancellationToken.None);
        stream.Position = 0;

        // Assert
        Assert.That(stream.ToArray(), Is.EqualTo(expected));
    }

    [Theory]
    [TestCase(15293ul, new byte[] { 0x7B, 0xBD })]
    [TestCase(16383ul, new byte[] { 0x7F, 0xFF })]
    public async Task WriteVarInt_TwoBytes_WritesCorrectValue(ulong value, byte[] expected)
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new Http3Writer(stream);
        var buffer = new byte[8];

        // Act
        await writer.WriteVarInt(value, buffer, CancellationToken.None);
        stream.Position = 0;

        // Assert
        Assert.That(stream.ToArray(), Is.EqualTo(expected));
    }

    [Theory]
    [TestCase(494878333ul, new byte[] { 0x9D, 0x7F, 0x3E, 0x7D })]
    [TestCase(1073741823ul, new byte[] { 0xBF, 0xFF, 0xFF, 0xFF })]
    public async Task WriteVarInt_FourBytes_WritesCorrectValue(ulong value, byte[] expected)
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new Http3Writer(stream);
        var buffer = new byte[8];

        // Act
        await writer.WriteVarInt(value, buffer, CancellationToken.None);
        stream.Position = 0;

        // Assert
        Assert.That(stream.ToArray(), Is.EqualTo(expected));
    }

    [Theory]
    [TestCase(151288809941952652ul, new byte[] { 0xC2, 0x19, 0x7C, 0x5E, 0xFF, 0x14, 0xE8, 0x8C })]
    [TestCase(4611686018427387903ul, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    public async Task WriteVarInt_EightBytes_WritesCorrectValue(ulong value, byte[] expected)
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new Http3Writer(stream);
        var buffer = new byte[8];

        // Act
        await writer.WriteVarInt(value, buffer, CancellationToken.None);
        stream.Position = 0;

        // Assert
        Assert.That(stream.ToArray(), Is.EqualTo(expected));
    }

    [Test]
    public async Task WriteVarInt_ValueTooLarge_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new Http3Writer(stream);
        var buffer = new byte[8];
        var value = 4611686018427387904UL; // Max + 1

        // Act & Assert
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await writer.WriteVarInt(value, buffer, CancellationToken.None));
    }
}