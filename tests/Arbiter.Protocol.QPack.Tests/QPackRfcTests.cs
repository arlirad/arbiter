using Arlirad.Infrastructure.QPack.Decoding;
using Arlirad.Infrastructure.QPack.Models;
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

        var timeouter = new CancellationTokenSource();
        timeouter.CancelAfter(TimeSpan.FromMilliseconds(1000));

        var buffers = await RFCHelper.GetRfcExampleBuffers(example);
        var buffer0Headers = new HttpHeaders();

        await using (var buffer0Section = await decoder.GetSectionReader(streamId: 0, buffers[0], buffers[0].Length,
            timeouter.Token))
        {
            foreach (var field in buffer0Section)
            {
                buffer0Headers[field.Name] = field.Value;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(buffer0Headers[":path"], Is.EqualTo("/index.html"));
            Assert.That(decoderInstructions.Length, Is.EqualTo(0));
        });
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

        var buffers = await RFCHelper.GetRfcExampleBuffers(example);

        await encoderInstructions.WriteAsync(buffers[RFCHelper.EncoderStream].ToArray());

        var headers = new HttpHeaders();

        await using (var stream4Section = await decoder.GetSectionReader(streamId: 4, buffers[4],
            length: buffers[4].Length))
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
            Assert.That(decoder.TotalInsertCount, Is.EqualTo(2));
            Assert.That(decoder.GetDynamicTable(), Is.EqualTo(new List<QPackField>
            {
                new(":authority", "www.example.com"),
                new(":path", "/sample/path"),
            }));

            Assert.That(headers[":authority"], Is.EqualTo("www.example.com"));
            Assert.That(headers[":path"], Is.EqualTo("/sample/path"));
            Assert.That(buffer[0], Is.EqualTo(buffers[RFCHelper.DecoderStream][0]));
            Assert.That(decoderInstructions.Length, Is.EqualTo(0));
        });
    }

    /// <summary>
    /// The encoder inserts a header into the dynamic table with a literal name. The decoder acknowledges receipt of the
    /// entry. The encoder does not send any encoded field sections.
    /// </summary>
    public static async Task SpeculativeInsert(
        QueueStream encoderInstructions,
        QueueStream decoderInstructions,
        QPackDecoder decoder
    )
    {
        const string example = """
                               Stream: Encoder
                               4a63 7573 746f 6d2d | Insert With Literal Name
                               6b65 790c 6375 7374 |  (custom-key=custom-value)
                               6f6d 2d76 616c 7565 |

                                                             Abs Ref Name        Value
                                                              0   0  :authority  www.example.com
                                                              1   0  :path       /sample/path
                                                             ^-- acknowledged --^
                                                              2   0  custom-key  custom-value
                                                             Size=160

                               Stream: Decoder
                               01                  | Insert Count Increment (1)

                                                             Abs Ref Name        Value
                                                              0   0  :authority  www.example.com
                                                              1   0  :path       /sample/path
                                                              2   0  custom-key  custom-value
                                                             ^-- acknowledged --^
                                                             Size=160
                               """;

        var timeouter = new CancellationTokenSource();
        timeouter.CancelAfter(TimeSpan.FromMilliseconds(1000));

        var buffers = await RFCHelper.GetRfcExampleBuffers(example);

        await encoderInstructions.WriteAsync(buffers[RFCHelper.EncoderStream].ToArray(), timeouter.Token);

        var buffer = new byte[1];

        await decoderInstructions.ReadExactlyAsync(new Memory<byte>(buffer), timeouter.Token);

        Assert.Multiple(() =>
        {
            Assert.That(decoder.TotalInsertCount, Is.EqualTo(3));
            Assert.That(decoder.GetDynamicTable(), Is.EqualTo(new List<QPackField>
            {
                new(":authority", "www.example.com"),
                new(":path", "/sample/path"),
                new("custom-key", "custom-value"),
            }));

            Assert.That(buffer[0], Is.EqualTo(buffers[RFCHelper.DecoderStream][0]));
            Assert.That(decoderInstructions.Length, Is.EqualTo(0));
        });
    }

    /// <summary>
    /// The encoder duplicates an existing entry in the dynamic table, then sends an encoded field section referencing
    /// the dynamic table entries including the duplicated entry. The packet containing the encoder stream data is
    /// delayed. Before the packet arrives, the decoder cancels the stream and notifies the encoder that the encoded
    /// field section was not processed.
    /// </summary>
    public static async Task DuplicateInstructionStreamCancellation(
        QueueStream encoderInstructions,
        QueueStream decoderInstructions,
        QPackDecoder decoder
    )
    {
        const string example = """
                               Stream: Encoder
                               02                  | Duplicate (Relative Index = 2)
                                                   |  Absolute Index =
                                                   |   Insert Count(3) - Index(2) - 1 = 0

                                                             Abs Ref Name        Value
                                                              0   0  :authority  www.example.com
                                                              1   0  :path       /sample/path
                                                              2   0  custom-key  custom-value
                                                             ^-- acknowledged --^
                                                              3   0  :authority  www.example.com
                                                             Size=217

                               Stream: 8
                               0500                | Required Insert Count = 4, Base = 4
                               80                  | Indexed Field Line, Dynamic Table
                                                   |  Absolute Index = Base(4) - Index(0) - 1 = 3
                                                   |  (:authority=www.example.com)
                               c1                  | Indexed Field Line, Static Table Index = 1
                                                   |  (:path=/)
                               81                  | Indexed Field Line, Dynamic Table
                                                   |  Absolute Index = Base(4) - Index(1) - 1 = 2
                                                   |  (custom-key=custom-value)

                                                             Abs Ref Name        Value
                                                              0   0  :authority  www.example.com
                                                              1   0  :path       /sample/path
                                                              2   1  custom-key  custom-value
                                                             ^-- acknowledged --^
                                                              3   1  :authority  www.example.com
                                                             Size=217

                               Stream: Decoder
                               48                  | Stream Cancellation (Stream=8)
                               01                  | Increment count ack (not in the RFC)

                                                             Abs Ref Name        Value
                                                              0   0  :authority  www.example.com
                                                              1   0  :path       /sample/path
                                                              2   0  custom-key  custom-value
                                                             ^-- acknowledged --^
                                                              3   0  :authority  www.example.com
                                                             Size=217
                               """;

        var timeouter = new CancellationTokenSource();
        timeouter.CancelAfter(TimeSpan.FromMilliseconds(1000));

        var buffers = await RFCHelper.GetRfcExampleBuffers(example);
        var cts = new CancellationTokenSource();

        cts.CancelAfter(TimeSpan.FromMilliseconds(250));

        var readerTask = decoder.GetSectionReader(8, buffers[8], buffers[8].Length, cts.Token);

        while (!cts.IsCancellationRequested)
        {
            await Task.Delay(25, CancellationToken.None);
        }

        try
        {
            await readerTask;
        }
        catch (Exception ex)
        {
            Assert.That(ex.GetType(), Is.EqualTo(typeof(OperationCanceledException)));
        }

        await encoderInstructions.WriteAsync(buffers[RFCHelper.EncoderStream], CancellationToken.None);

        var buffer = new byte[2];

        await decoderInstructions.ReadExactlyAsync(new Memory<byte>(buffer), timeouter.Token);

        Assert.Multiple(() =>
        {
            Assert.That(decoder.TotalInsertCount, Is.EqualTo(4));
            Assert.That(decoder.GetDynamicTable(), Is.EqualTo(new List<QPackField>
            {
                new(":authority", "www.example.com"),
                new(":path", "/sample/path"),
                new("custom-key", "custom-value"),
                new(":authority", "www.example.com"),
            }));

            Assert.That(buffer[0], Is.EqualTo(buffers[RFCHelper.DecoderStream][0]));
            Assert.That(buffer[1], Is.EqualTo(buffers[RFCHelper.DecoderStream][1]));
            Assert.That(decoderInstructions.Length, Is.EqualTo(0));
        });
    }

    public static async Task DynamicTableInsertEviction(
        QueueStream encoderInstructions,
        QueueStream decoderInstructions,
        QPackDecoder decoder
    )
    {
        const string example = """
                               Stream: Encoder
                               810d 6375 7374 6f6d | Insert With Name Reference
                               2d76 616c 7565 32   |  Dynamic Table, Relative Index = 1
                                                   |  Absolute Index =
                                                   |   Insert Count(4) - Index(1) - 1 = 2
                                                   |  (custom-key=custom-value2)

                                                             Abs Ref Name        Value
                                                              1   0  :path       /sample/path
                                                              2   0  custom-key  custom-value
                                                             ^-- acknowledged --^
                                                              3   0  :authority  www.example.com
                                                              4   0  custom-key  custom-value2
                                                             Size=215
                                                             
                               Stream: Decoder
                               01                  | Insert Count Increment (1) (not in the RFC)
                               """;

        var timeouter = new CancellationTokenSource();
        timeouter.CancelAfter(TimeSpan.FromMilliseconds(1000));

        var buffers = await RFCHelper.GetRfcExampleBuffers(example);

        await encoderInstructions.WriteAsync(buffers[RFCHelper.EncoderStream].ToArray(), timeouter.Token);

        var buffer = new byte[1];

        await decoderInstructions.ReadExactlyAsync(new Memory<byte>(buffer), timeouter.Token);

        Assert.Multiple(() =>
        {
            Assert.That(decoder.TotalInsertCount, Is.EqualTo(5));
            Assert.That(decoder.DynamicTableSize, Is.EqualTo(215));
            Assert.That(decoder.GetDynamicTable(), Is.EqualTo(new List<QPackField>
            {
                new(":path", "/sample/path"),
                new("custom-key", "custom-value"),
                new(":authority", "www.example.com"),
                new("custom-key", "custom-value2"),
            }));

            Assert.That(buffer[0], Is.EqualTo(buffers[RFCHelper.DecoderStream][0]));
            Assert.That(decoderInstructions.Length, Is.EqualTo(0));
        });
    }
}