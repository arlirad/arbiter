using Arlirad.Http3.Streams;

namespace Arbiter.Http3.Tests;

public class Http3ReaderWriterRoundTripTests
{
    [Theory]
    [TestCase(0ul)]
    [TestCase(1ul)]
    [TestCase(37ul)]
    [TestCase(63ul)]
    [TestCase(64ul)]
    [TestCase(16383ul)]
    [TestCase(16384ul)]
    [TestCase(1073741823ul)]
    [TestCase(1073741824ul)]
    [TestCase(4611686018427387903ul)]
    public async Task RoundTrip_WriteAndRead_PreservesValue(ulong originalValue)
    {
        // Arrange
        using var stream = new MemoryStream();
        var writeBuffer = new byte[8];
        var readBuffer = new byte[8];

        // Write to stream
        var writer = new Http3Writer(stream);
        await writer.WriteVarInt(originalValue, writeBuffer, CancellationToken.None);
        stream.Position = 0;

        // Act - Read back
        var reader = new Http3Reader(stream);
        var result = await reader.ReadVarInt(readBuffer, CancellationToken.None);

        // Assert
        Assert.That(result, Is.EqualTo((long)originalValue));
    }
}