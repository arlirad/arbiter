using Arlirad.QPack.Streams;

namespace Arlirad.QPack.Tests;

public class BitStreamTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void BitStreamMisalignedTest()
    {
        var data = new byte[]
        {
            0xE7, 0xF9, 0xC0,
        };

        var stream = new BitStream(data);

        var result1 = stream.ReadNotAdvancing(5);
        Assert.That(result1, Is.EqualTo(28));
        stream.Position += 5;

        var result2 = stream.ReadNotAdvancing(5);
        Assert.That(result2, Is.EqualTo(31));
        stream.Position = 4;

        var result3 = stream.ReadNotAdvancing(16);
        Assert.That(result3, Is.EqualTo(0x7F9C));
    }
}