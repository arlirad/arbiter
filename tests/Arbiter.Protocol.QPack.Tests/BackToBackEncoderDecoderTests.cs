using System.Text;
using Arlirad.Infrastructure.QPack.Common;
using Arlirad.Infrastructure.QPack.Decoding;
using Arlirad.Infrastructure.QPack.Encoding;
using Arlirad.Infrastructure.QPack.Streams;
using Arlirad.QPack.Tests.Streams;

namespace Arlirad.QPack.Tests;

public class BackToBackEncoderDecoderTests
{
    private static async Task<(QueueStream encToDec, QueueStream decToEnc, QPackEncoder enc, QPackDecoder dec)>
        MakePair()
    {
        var encoderInstructions = new QueueStream();
        var decoderInstructions = new QueueStream();

        var encoder = new QPackEncoder();
        var decoder = new QPackDecoder { InsertCountIncrementDelay = TimeSpan.Zero };

        await encoder.Start();
        await decoder.Start();

        encoder.SetOutgoingStream(encoderInstructions);
        decoder.SetIncomingStream(encoderInstructions);

        decoder.SetOutgoingStream(decoderInstructions);
        encoder.SetIncomingStream(decoderInstructions);

        return (encoderInstructions, decoderInstructions, encoder, decoder);
    }

    [Test]
    public async Task FieldSection_RoundTrip_StaticAndLiteral_ZeroRequiredInsertCount()
    {
        var (_, decToEnc, encoder, decoder) = await MakePair();

        var sectionStream = new MemoryStream();

        await using (var writer = await encoder.GetSectionWriter(streamId: 0, stream: sectionStream,
            ct: CancellationToken.None))
        {
            await writer.WritePrefix(CancellationToken.None);
            await writer.Write(":path", "/index.html", CancellationToken.None);
            await writer.Write("custom-key", "custom-value", CancellationToken.None);
        }

        var sectionBytes = sectionStream.ToArray();

        var headers = new Dictionary<string, string>();

        await using (var reader = await decoder.GetSectionReader(streamId: 0, buffer: sectionBytes,
            length: sectionBytes.Length, ct: CancellationToken.None))
        {
            foreach (var field in reader)
                headers[field.Name] = field.Value!;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(headers[":path"], Is.EqualTo("/index.html"));
            Assert.That(headers["custom-key"], Is.EqualTo("custom-value"));
            Assert.That(decToEnc.Length, Is.Zero, "No decoder instruction expected for RIC=0");
        }
    }

    [Test]
    public async Task FieldSection_WriteFieldSection_StaticAndLiteral_RoundTrip()
    {
        var (_, _, encoder, decoder) = await MakePair();
        encoder.Initialize(4096, 0);

        var sectionStream = new MemoryStream();

        await using (var writer = await encoder.GetSectionWriter(streamId: 0, stream: sectionStream,
            ct: CancellationToken.None))
        {
            await writer.WriteFieldSection([
                (":path", "/index.html"),
                ("custom-key", "custom-value"),
            ], CancellationToken.None);
        }

        var sectionBytes = sectionStream.ToArray();

        var headers = new Dictionary<string, string>();

        await using (var reader = await decoder.GetSectionReader(streamId: 0, buffer: sectionBytes,
            length: sectionBytes.Length, ct: CancellationToken.None))
        {
            foreach (var field in reader)
                headers[field.Name] = field.Value!;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(headers[":path"], Is.EqualTo("/index.html"));
            Assert.That(headers["custom-key"], Is.EqualTo("custom-value"));
        }
    }

    [Test]
    public async Task EncoderDecoder_Talk_InsertWithLiteralName_IncrementBackChannel()
    {
        var (encToDec, decToEnc, encoder, decoder) = await MakePair();

        var writer = new QPackWriter(encToDec);

        await writer.WritePrefixedIntAsync(220, 5, QPackConsts.EncoderInstructionDynamicTableCapacity);

        const string name = "custom-key";
        const string value = "custom-value";
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);

        await writer.WritePrefixedIntAsync(nameBytes.Length, 5, QPackConsts.EncoderInstructionInsertWithLiteralName,
            CancellationToken.None);

        await encToDec.WriteAsync(nameBytes);
        await writer.WritePrefixedIntAsync(valueBytes.Length, 7, 0b0000_0000);
        await encToDec.WriteAsync(valueBytes);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (decoder.TotalInsertCount < 1 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decoder.TotalInsertCount, Is.GreaterThanOrEqualTo(1), "Decoder should have processed the insert");
            Assert.That(decoder.GetDynamicTable().Any(f => f.Name == name && f.Value == value), Is.True);
        }

        deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (encoder.AckedInsertCount < 1 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.That(encoder.AckedInsertCount, Is.GreaterThanOrEqualTo(1), "Encoder should have received Insert Count Increment");
    }

    [Test]
    public async Task WriteFieldSection_WithHuffman_RoundTrip()
    {
        var (_, _, encoder, decoder) = await MakePair();
        encoder.Initialize(4096, 0);

        var sectionStream = new MemoryStream();

        await using (var writer = await encoder.GetSectionWriter(streamId: 0, stream: sectionStream,
            ct: CancellationToken.None))
        {
            await writer.WriteFieldSection([
                (":path", "/index.html"),
                ("content-type", "application/json"),
                ("cache-control", "no-cache"),
                ("accept-encoding", "gzip, deflate, br"),
            ], CancellationToken.None);
        }

        var sectionBytes = sectionStream.ToArray();

        var headers = new Dictionary<string, string>();

        await using (var reader = await decoder.GetSectionReader(streamId: 0, buffer: sectionBytes,
            length: sectionBytes.Length, ct: CancellationToken.None))
        {
            foreach (var field in reader)
                headers[field.Name] = field.Value!;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(headers[":path"], Is.EqualTo("/index.html"));
            Assert.That(headers["content-type"], Is.EqualTo("application/json"));
            Assert.That(headers["cache-control"], Is.EqualTo("no-cache"));
            Assert.That(headers["accept-encoding"], Is.EqualTo("gzip, deflate, br"));
        }
    }
}
