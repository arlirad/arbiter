using Arbiter.DTOs;
using Arlirad.QPack.Decoding;
using Arlirad.QPack.Tests.Helpers;
using Arlirad.QPack.Tests.Streams;

namespace Arlirad.QPack.Tests;

public class QPackRfcTests
{
    /// <summary>
    /// B.1. Literal Field Line with Name Reference
    /// The encoder sends an encoded field section containing a literal representation of a field with a static name
    /// reference.
    /// </summary>
    public static async Task LiteralFieldLineWithNameReference(
        QueueStream encoderInstructions,
        QueueStream decoderInstructions,
        QPackDecoder decoder
    )
    {
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

        var streams = await RFCHelper.GetRfcExampleStreams(example);
        var stream0Section = await decoder.GetSectionReader(streamId: 0, streams[0]);
        var stream0Headers = new HttpHeaders();

        foreach (var field in stream0Section)
        {
            stream0Headers[field.Name] = field.Value;
        }

        Assert.That(stream0Headers[":path"], Is.EqualTo("/index.html"));
    }

    /// <summary>
    /// The encoder sets the dynamic table capacity, inserts a header with a dynamic name reference, then sends a
    /// potentially blocking, encoded field section referencing this new entry. The decoder acknowledges processing the
    /// encoded field section, which implicitly acknowledges all dynamic table insertions up to the Required Insert
    /// Count.
    /// </summary>
    public static async Task DynamicTable(
        QueueStream encoderInstructions,
        QueueStream decoderInstructions,
        QPackDecoder decoder
    )
    {
        const string example = """
                               Stream: Encoder
                               3fbd01              | Set Dynamic Table Capacity=220
                               c00f 7777 772e 6578 | Insert With Name Reference
                               616d 706c 652e 636f | Static Table, Index=0
                               6d                  |  (:authority=www.example.com)
                               c10c 2f73 616d 706c | Insert With Name Reference
                               652f 7061 7468      |  Static Table, Index=1
                                                   |  (:path=/sample/path)

                                                             Abs Ref Name        Value
                                                             ^-- acknowledged --^
                                                              0   0  :authority  www.example.com
                                                              1   0  :path       /sample/path
                                                             Size=106

                               Stream: 4
                               0381                | Required Insert Count = 2, Base = 0
                               10                  | Indexed Field Line With Post-Base Index
                                                   |  Absolute Index = Base(0) + Index(0) = 0
                                                   |  (:authority=www.example.com)
                               11                  | Indexed Field Line With Post-Base Index
                                                   |  Absolute Index = Base(0) + Index(1) = 1
                                                   |  (:path=/sample/path)

                                                             Abs Ref Name        Value
                                                             ^-- acknowledged --^
                                                              0   1  :authority  www.example.com
                                                              1   1  :path       /sample/path
                                                             Size=106

                               Stream: Decoder
                               84                  | Section Acknowledgment (stream=4)

                                                             Abs Ref Name        Value
                                                              0   0  :authority  www.example.com
                                                              1   0  :path       /sample/path
                                                             ^-- acknowledged --^
                                                             Size=106
                               """;

        var streams = await RFCHelper.GetRfcExampleStreams(example);

        await encoderInstructions.WriteAsync(streams[RFCHelper.EncoderStream].ToArray());

        var headers = new HttpHeaders();

        await using (var stream4Section = await decoder.GetSectionReader(streamId: 4, streams[4]))
        {
            Assert.That(stream4Section.Base, Is.EqualTo(0));

            foreach (var field in stream4Section)
            {
                headers[field.Name] = field.Value;
            }
        }

        var buffer = new byte[1];

        await decoderInstructions.ReadExactlyAsync(new Memory<byte>(buffer), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decoder.DynamicTableCapacity, Is.EqualTo(220));
            Assert.That(decoder.DynamicTableSize, Is.EqualTo(106));
            Assert.That(headers[":authority"], Is.EqualTo("www.example.com"));
            Assert.That(headers[":path"], Is.EqualTo("/sample/path"));
            Assert.That(buffer[0], Is.EqualTo(0x84));
        });
    }
}