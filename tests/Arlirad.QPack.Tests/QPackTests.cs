using Arlirad.QPack.Decoding;
using Arlirad.QPack.Streams;
using Arlirad.QPack.Tests.Streams;

namespace Arlirad.QPack.Tests;

public class QPackTests
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
    public void VarIntReadTest()
    {
        var stream10 = new QPackReader(new MemoryStream(_integers[10]));
        Assert.That(stream10.ReadPrefixedInt(5), Is.EqualTo(10));

        var stream1337 = new QPackReader(new MemoryStream(_integers[1337]));
        Assert.That(stream1337.ReadPrefixedInt(5), Is.EqualTo(1337));

        var stream42 = new QPackReader(new MemoryStream(_integers[42]));
        Assert.That(stream42.ReadPrefixedInt(8), Is.EqualTo(42));
    }

    [Test]
    public async Task VarIntWriteTest()
    {
        var stream10 = new MemoryStream();
        await (new QPackWriter(stream10)).WritePrefixedIntAsync(10, 5, 0b1110_0000, CancellationToken.None);
        Assert.That(stream10.ToArray(), Is.EqualTo(_integers[10]));

        var stream1337 = new MemoryStream();
        await (new QPackWriter(stream1337)).WritePrefixedIntAsync(1337, 5, 0b1110_0000, CancellationToken.None);
        Assert.That(stream1337.ToArray(), Is.EqualTo(_integers[1337]));

        var stream42 = new MemoryStream();
        await (new QPackWriter(stream42)).WritePrefixedIntAsync(42, 8, 0b0000_0000, CancellationToken.None);
        Assert.That(stream42.ToArray(), Is.EqualTo(_integers[42]));
    }

    [Test]
    public async Task BackToBackWriteReadTest()
    {
        for (var i = 0; i < 65556; i++)
        {
            var prefix = i % 8 + 1;
            var stream = new MemoryStream();
            var reader = new QPackReader(stream);
            var writer = new QPackWriter(stream);

            await writer.WritePrefixedIntAsync(i, prefix, 0b0000_0000, CancellationToken.None);

            stream.Position = 0;

            Assert.That(reader.ReadPrefixedInt(prefix), Is.EqualTo(i));
        }
    }

    [Test]
    public async Task RfcTests()
    {
        var encoderInstructions = new QueueStream();
        var decoderInstructions = new QueueStream();
        var decoder = new QPackDecoder(encoderInstructions, decoderInstructions);

        await decoder.Start();

        await QPackRfcTests.LiteralFieldLineWithNameReference(encoderInstructions, decoderInstructions, decoder);
        await QPackRfcTests.DynamicTable(encoderInstructions, decoderInstructions, decoder);
    }
}