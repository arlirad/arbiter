namespace Arlirad.QPack.Tests;

public class Tests
{
    private readonly Dictionary<int, byte[]> _integers = new()
    {
        [10] = [0b1110_1010],
        [42] = [0b0010_1010],
        [1337] = [0b1111_1111, 0b1001_1010, 0b0000_1010],
    };

    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void QPackVarIntReadTest()
    {
        var stream10 = new QPackStream(new MemoryStream(_integers[10]));
        Assert.That(stream10.ReadVarInt(5), Is.EqualTo(10));

        var stream1337 = new QPackStream(new MemoryStream(_integers[1337]));
        Assert.That(stream1337.ReadVarInt(5), Is.EqualTo(1337));

        var stream42 = new QPackStream(new MemoryStream(_integers[42]));
        Assert.That(stream42.ReadVarInt(8), Is.EqualTo(42));
    }
}