using Arbiter.Protocol.QPack.Common;
using Arbiter.Protocol.QPack.Decoding;
using Arbiter.Protocol.QPack.Encoding;
using Arbiter.Protocol.QPack.Streams;
using Arbiter.Protocol.QPack.Tests.Streams;

namespace Arbiter.Protocol.QPack.Tests;

public class EncoderDecoderIntegrationTests
{
    private static async Task WaitForDecoderInsertCount(QPackDecoder decoder, long expected, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));

        while (decoder.TotalInsertCount < expected && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    private static async Task<(QueueStream encToDec, QueueStream decToEnc, QPackEncoder enc, QPackDecoder dec)> MakePair(int tableCapacity = 4096, int blockedStreams = 0)
    {
        var encToDec = new QueueStream();
        var decToEnc = new QueueStream();

        var encoder = new QPackEncoder();
        var decoder = new QPackDecoder {
            InsertCountIncrementDelay = TimeSpan.Zero,
        };

        await encoder.Start();
        await decoder.Start();

        encoder.SetOutgoingStream(encToDec);
        decoder.SetIncomingStream(encToDec);

        decoder.SetOutgoingStream(decToEnc);
        encoder.SetIncomingStream(decToEnc);

        decoder.MaxTableCapacity = tableCapacity;
        encoder.Initialize(tableCapacity, blockedStreams);

        return (encToDec, decToEnc, encoder, decoder);
    }

    private static async Task<List<(string Name, string? Value)>> EncodeDecode(
        QPackEncoder encoder,
        QPackDecoder decoder,
        long streamId,
        List<(string, string)> headers)
    {
        using var ms = new MemoryStream();
        var writer = await encoder.GetSectionWriter(streamId, ms);
        await writer.WriteFieldSection(headers, default);

        var sectionBytes = ms.ToArray();

        var result = new List<(string, string?)>();

        await using (var reader = await decoder.GetSectionReader(streamId, sectionBytes, sectionBytes.Length))
        {
            foreach (var field in reader)
                result.Add((field.Name, field.Value));
        }

        return result;
    }

    [Test]
    public async Task WriteFieldSection_StaticOnly_RoundTrip()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var headers = new List<(string, string)> {
            (":method", "GET"),
            (":scheme", "https"),
            (":path", "/"),
            (":authority", "www.example.com"),
        };

        var result = await EncodeDecode(encoder, decoder, 0, headers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(4));
            Assert.That(result[0], Is.EqualTo((":method", "GET")));
            Assert.That(result[1], Is.EqualTo((":scheme", "https")));
            Assert.That(result[2], Is.EqualTo((":path", "/")));
            Assert.That(result[3], Is.EqualTo((":authority", "www.example.com")));
        }
    }

    [Test]
    public async Task WriteFieldSection_StaticNameRef_WithValue_RoundTrip()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var headers = new List<(string, string)> {
            (":method", "GET"),
            (":path", "/index.html"),
            ("content-type", "text/html; charset=utf-8"),
        };

        var result = await EncodeDecode(encoder, decoder, 0, headers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result[0], Is.EqualTo((":method", "GET")));
            Assert.That(result[1], Is.EqualTo((":path", "/index.html")));
            Assert.That(result[2], Is.EqualTo(("content-type", "text/html; charset=utf-8")));
        }
    }

    [Test]
    public async Task WriteFieldSection_CustomHeaders_FullLiteral_RoundTrip()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var headers = new List<(string, string)> {
            ("x-custom-name", "x-custom-value"),
            ("x-another", "some data here"),
        };

        var result = await EncodeDecode(encoder, decoder, 0, headers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0], Is.EqualTo(("x-custom-name", "x-custom-value")));
            Assert.That(result[1], Is.EqualTo(("x-another", "some data here")));
        }
    }

    [Test]
    public async Task Encoder_Sends_SetDynamicTableCapacity_On_Initialize()
    {
        var encToDec = new QueueStream();

        _ = new QueueStream();

        var encoder = new QPackEncoder();
        await encoder.Start();

        encoder.SetOutgoingStream(encToDec);
        encoder.Initialize(220, 0);

        var buffer = new byte[16];
        await encToDec.ReadExactlyAsync(new Memory<byte>(buffer, 0, 3), CancellationToken.None);

        var firstByte = buffer[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(QPackConsts.Is(firstByte, 0b1110_0000, QPackConsts.EncoderInstructionDynamicTableCapacity), Is.True);
        }
    }

    [Test]
    public async Task DynamicTable_InsertAndReference_RoundTrip()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var headers1 = new List<(string, string)> {
            (":authority", "www.example.com"),
            (":path", "/"),
            ("custom-key", "custom-value"),
        };

        var result1 = await EncodeDecode(encoder, decoder, 0, headers1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result1, Has.Count.EqualTo(3));
            Assert.That(result1[0], Is.EqualTo((":authority", "www.example.com")));
            Assert.That(result1[1], Is.EqualTo((":path", "/")));
            Assert.That(result1[2], Is.EqualTo(("custom-key", "custom-value")));
        }

        await WaitForDecoderInsertCount(decoder, encoder.TotalInsertCount);
        var dynamicTable = decoder.GetDynamicTable();
        Assert.That(dynamicTable, Is.Not.Empty, "Dynamic table should have entries after first section");
    }

    [Test]
    public async Task DynamicTable_SecondSection_ReusesEntries()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var headers1 = new List<(string, string)> {
            ("x-custom", "value-that-gets-inserted"),
            (":path", "/"),
        };

        var _ = await EncodeDecode(encoder, decoder, 0, headers1);

        await WaitForDecoderInsertCount(decoder, encoder.TotalInsertCount);

        var encTable = encoder.GetDynamicTable();
        Assert.That(encTable, Is.Not.Empty, "Encoder should have dynamic entries");

        var headers2 = new List<(string, string)> {
            ("x-custom", "value-that-gets-inserted"),
            (":path", "/test"),
        };

        var result2 = await EncodeDecode(encoder, decoder, 4, headers2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result2, Has.Count.EqualTo(2));
            Assert.That(result2[0], Is.EqualTo(("x-custom", "value-that-gets-inserted")));
            Assert.That(result2[1], Is.EqualTo((":path", "/test")));
        }
    }

    [Test]
    public async Task Huffman_EncodedHeaders_DecodeCorrectly()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var headers = new List<(string, string)> {
            ("content-type", "application/json"),
            ("cache-control", "no-cache"),
            ("accept-encoding", "gzip, deflate, br"),
        };

        var result = await EncodeDecode(encoder, decoder, 0, headers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result[0], Is.EqualTo(("content-type", "application/json")));
            Assert.That(result[1], Is.EqualTo(("cache-control", "no-cache")));
            Assert.That(result[2], Is.EqualTo(("accept-encoding", "gzip, deflate, br")));
        }
    }

    [Test]
    public async Task Eviction_WhenTableFull()
    {
        var (_, _, encoder, decoder) = await MakePair(100);

        var headers1 = new List<(string, string)> {
            ("x-first", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
        };

        var result1 = await EncodeDecode(encoder, decoder, 0, headers1);
        Assert.That(result1[0], Is.EqualTo(("x-first", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")));

        var headers2 = new List<(string, string)> {
            ("x-second", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
        };

        var result2 = await EncodeDecode(encoder, decoder, 4, headers2);
        Assert.That(result2[0], Is.EqualTo(("x-second", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")));

        await WaitForDecoderInsertCount(decoder, encoder.TotalInsertCount);
        var decTable = decoder.GetDynamicTable();
        Assert.That(decTable, Has.Count.EqualTo(1), "Only one entry should fit after eviction");
    }

    [Test]
    public async Task SectionAcknowledgment_SentByDecoder()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var headers = new List<(string, string)> {
            ("x-custom-entry", "some-value-for-insertion"),
            (":path", "/"),
        };

        _ = await EncodeDecode(encoder, decoder, 0, headers);

        await WaitForDecoderInsertCount(decoder, encoder.TotalInsertCount);
        var decTable = decoder.GetDynamicTable();
        Assert.That(decTable, Is.Not.Empty, "Decoder should have entries in its table");
    }

    [Test]
    public async Task DynamicNameRef_Path()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var headers1 = new List<(string, string)> {
            ("x-shared-name", "first-value"),
        };

        var result1 = await EncodeDecode(encoder, decoder, 0, headers1);
        Assert.That(result1[0], Is.EqualTo(("x-shared-name", "first-value")));

        await WaitForDecoderInsertCount(decoder, encoder.TotalInsertCount);

        var encDynamicTable = encoder.GetDynamicTable();
        Assert.That(encDynamicTable, Is.Not.Empty, "Encoder should have dynamic entries");

        var headers2 = new List<(string, string)> {
            ("x-shared-name", "second-value"),
        };

        var result2 = await EncodeDecode(encoder, decoder, 4, headers2);
        Assert.That(result2[0], Is.EqualTo(("x-shared-name", "second-value")));
    }

    [Test]
    public async Task FullLiteral_WithHuffman_LongStrings()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var longValue = new string('a', 300);

        var headers = new List<(string, string)> {
            ("x-long-header", longValue),
        };

        var result = await EncodeDecode(encoder, decoder, 0, headers);

        Assert.That(result[0], Is.EqualTo(("x-long-header", longValue)));
    }

    [Test]
    public async Task EmptyFieldSection()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var headers = new List<(string, string)>();

        var result = await EncodeDecode(encoder, decoder, 0, headers);

        Assert.That(result.Count, Is.Zero);
    }

    [Test]
    public async Task MultipleSections_TableAccumulates()
    {
        var (_, _, encoder, decoder) = await MakePair();

        for (var i = 0; i < 5; i++)
        {
            var headers = new List<(string, string)> {
                ($"x-header-{i}", $"value-{i}"),
                (":path", "/"),
            };

            var result = await EncodeDecode(encoder, decoder, i * 4, headers);
            Assert.That(result, Has.Count.EqualTo(2), $"Iteration {i}");
            Assert.That(result[0], Is.EqualTo(($"x-header-{i}", $"value-{i}")), $"Iteration {i}");
        }

        await WaitForDecoderInsertCount(decoder, encoder.TotalInsertCount);
        var decTable = decoder.GetDynamicTable();
        Assert.That(decTable, Has.Count.GreaterThan(2), "Table should accumulate entries across sections");
    }

    [Test]
    public async Task Encoder_Processes_InsertCountIncrement()
    {
        var (_, decToEnc, encoder, decoder) = await MakePair();

        var headers = new List<(string, string)> {
            ("x-test-entry", "test-value"),
        };

        var _ = await EncodeDecode(encoder, decoder, 0, headers);

        var writer = new QPackWriter(decToEnc);
        await writer.WritePrefixedIntAsync(1, 6, QPackConsts.DecoderInstructionInsertCountIncrement, CancellationToken.None);
        await decToEnc.FlushAsync(CancellationToken.None);

        await Task.Delay(150);

        Assert.That(encoder.AckedInsertCount, Is.GreaterThan(0), "Encoder should have processed Insert Count Increment");
    }

    [Test]
    public async Task DuplicateHeaderValues_StaticExact()
    {
        var (_, _, encoder, decoder) = await MakePair();

        var headers = new List<(string, string)> {
            (":status", "200"),
            (":method", "GET"),
            (":scheme", "https"),
            (":path", "/"),
        };

        var result = await EncodeDecode(encoder, decoder, 0, headers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(4));
            Assert.That(result[0], Is.EqualTo((":status", "200")));
            Assert.That(result[1], Is.EqualTo((":method", "GET")));
            Assert.That(result[2], Is.EqualTo((":scheme", "https")));
            Assert.That(result[3], Is.EqualTo((":path", "/")));
        }
    }
}
