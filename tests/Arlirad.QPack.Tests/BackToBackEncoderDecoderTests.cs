using System.Text;
using Arlirad.QPack.Common;
using Arlirad.QPack.Decoding;
using Arlirad.QPack.Encoding;
using Arlirad.QPack.Streams;
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
        var decoder = new QPackDecoder();

        // Start
        await encoder.Start();
        await decoder.Start();

        // Wire streams both ways
        // Encoder outgoing -> Decoder incoming
        encoder.SetOutgoingStream(encoderInstructions);
        decoder.SetIncomingStream(encoderInstructions);

        // Decoder outgoing -> Encoder incoming
        decoder.SetOutgoingStream(decoderInstructions);
        encoder.SetIncomingStream(decoderInstructions);

        return (encoderInstructions, decoderInstructions, encoder, decoder);
    }

    [Test]
    public async Task FieldSection_RoundTrip_StaticAndLiteral_ZeroRequiredInsertCount()
    {
        var (encToDec, decToEnc, encoder, decoder) = await MakePair();

        // Build a field section with Required Insert Count = 0, Base = 0
        var sectionStream = new MemoryStream();

        // Use the encoder's field section writer and write the encoded field section prefix first
        await using (var writer = await encoder.GetSectionWriter(streamId: 0, stream: sectionStream,
            ct: CancellationToken.None))
        {
            await writer.WritePrefix(CancellationToken.None);
            // Static exact match (:path=/)
            await writer.Write(":path", "/index.html", CancellationToken.None);
            // Literal name/value
            await writer.Write("custom-key", "custom-value", CancellationToken.None);
        }

        var sectionBytes = sectionStream.ToArray();

        var headers = new Dictionary<string, string>();

        await using (var reader = await decoder.GetSectionReader(streamId: 0, buffer: sectionBytes,
            length: sectionBytes.Length, ct: CancellationToken.None))
        {
            foreach (var field in reader)
            {
                headers[field.Name] = field.Value!;
            }
        }

        // With RequiredInsertCount = 0, the decoder must not send Section Acknowledgment
        Assert.Multiple(() =>
        {
            Assert.That(headers[":path"], Is.EqualTo("/index.html"));
            Assert.That(headers["custom-key"], Is.EqualTo("custom-value"));
            Assert.That(decToEnc.Length, Is.EqualTo(0), "No decoder→encoder instruction expected for RIC=0");
        });
    }

    [Test]
    public async Task EncoderDecoder_Talk_InsertWithLiteralName_IncrementBackChannel()
    {
        var (encToDec, decToEnc, encoder, decoder) = await MakePair();

        // Send Dynamic Table Capacity and an Insert With Literal Name from encoder→decoder
        var writer = new QPackWriter(encToDec);

        // Set Dynamic Table Capacity = 220 (enough for the entry below)
        await writer.WritePrefixedIntAsync(220, 5, QPackConsts.EncoderInstructionDynamicTableCapacity);

        const string name = "custom-key";
        const string value = "custom-value";
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);

        // Insert With Literal Name
        await writer.WritePrefixedIntAsync(nameBytes.Length, 5, QPackConsts.EncoderInstructionInsertWithLiteralName,
            CancellationToken.None);

        await encToDec.WriteAsync(nameBytes);
        await writer.WritePrefixedIntAsync(valueBytes.Length, 7, 0b0000_0000);
        await encToDec.WriteAsync(valueBytes);

        // Expect an Insert Count Increment from decoder -> encoder side after a short delay
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var firstByte = new byte[1];
        await decToEnc.ReadExactlyAsync(new Memory<byte>(firstByte), cts.Token);

        // Verify the decoder state and that at least one instruction byte was sent
        // (increment = one fits in a single byte)
        Assert.Multiple(() =>
        {
            Assert.That(decoder.TotalInsertCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(decoder.GetDynamicTable().Any(f => f.Name == name && f.Value == value), Is.True);
            // For small increments (1), the instruction is a single byte with the top two bits being 00
            Assert.That((firstByte[0] & 0b1100_0000), Is.EqualTo(QPackConsts.DecoderInstructionInsertCountIncrement));
        });
    }
}