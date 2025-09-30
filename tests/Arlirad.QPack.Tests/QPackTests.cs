using Arbiter.DTOs;
using Arlirad.QPack.Decoding;
using Arlirad.QPack.Streams;
using Arlirad.QPack.Tests.Helpers;

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
    public void QPackVarIntReadTest()
    {
        var stream10 = new QPackStream(new MemoryStream(_integers[10]));
        Assert.That(stream10.ReadVarInt(5), Is.EqualTo(10));

        var stream1337 = new QPackStream(new MemoryStream(_integers[1337]));
        Assert.That(stream1337.ReadVarInt(5), Is.EqualTo(1337));

        var stream42 = new QPackStream(new MemoryStream(_integers[42]));
        Assert.That(stream42.ReadVarInt(8), Is.EqualTo(42));
    }

    /// <summary>
    /// B.1. Literal Field Line with Name Reference
    /// The encoder sends an encoded field section containing a literal representation of a field with a static name
    /// reference.
    /// </summary>
    [Test]
    public void QPackLiteralFieldLineWithNameReferenceTest()
    {
        var reader = new QPackDecoder();
        const string example = """
                               Stream: 0
                               0000                | Required Insert Count = 0, Base = 0
                               510b 2f69 6e64 6578 | Literal Field Line with Name Reference
                               2e68 746d 6c        |  Static Table, Index=1
                                                   |  (:path=/index.html)

                                                             Abs Ref Name        Value
                                                             ^-- acknowledged --^
                                                             Size=0
                               """;

        var streams = RFCHelper.GetRfcExampleBytes(example);
        var stream0Section = reader.GetSectionReader(streamId: 0, streams[0]).GetAwaiter().GetResult();
        var stream0Headers = new HttpHeaders();

        foreach (var field in stream0Section)
        {
            stream0Headers[field.Name] = field.Value;
        }

        Assert.That(stream0Headers[":path"], Is.EqualTo("/index.html"));
    }
}