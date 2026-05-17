using Arlirad.Infrastructure.QPack.Huffman;

namespace Arlirad.QPack.Tests;

public class HuffmanRoundTripTests
{
    [Test]
    public void EncodeDecode_SingleBytes()
    {
        for (var b = 0; b <= 255; b++)
        {
            var input = new byte[] { (byte)b };
            var encoded = HPackHuffman.Encode(input);
            var decoded = HPackHuffman.Decode(encoded);

            Assert.That(decoded, Is.EqualTo(input), $"Failed for byte {b}");
        }
    }

    [Test]
    public void EncodeDecode_EmptyString()
    {
        var input = Array.Empty<byte>();
        var encoded = HPackHuffman.Encode(input);
        var decoded = HPackHuffman.Decode(encoded);

        Assert.That(decoded, Is.EqualTo(input));
    }

    [Test]
    public void EncodeDecode_CommonHeaderValues()
    {
        var testValues = new[] {
            "text/html",
            "application/json",
            "GET",
            "POST",
            "https",
            "http",
            "gzip, deflate, br",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            "en-US,en;q=0.9",
            "no-cache",
            "max-age=31536000",
            "*/*",
        };

        foreach (var testValue in testValues)
        {
            var input = System.Text.Encoding.UTF8.GetBytes(testValue);
            var encoded = HPackHuffman.Encode(input);
            var decoded = HPackHuffman.Decode(encoded);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(decoded, Is.EqualTo(input), $"Failed for: {testValue}");
                Assert.That(System.Text.Encoding.UTF8.GetString(decoded), Is.EqualTo(testValue));
            }
        }
    }

    [Test]
    public void Huffman_Is_Shorter_Than_Raw_ForTypicalStrings()
    {
        var testCases = new[] {
            ("text/html", true),
            ("application/json", true),
            ("GET", false),
            ("POST", false),
            ("en-US", true),
            ("no-cache", true),
            ("max-age=0", true),
            ("xyz", false),
            ("abcdefghijklmnopqrstuvwxyz", true),
        };

        foreach (var (value, expectedShorter) in testCases)
        {
            var input = System.Text.Encoding.UTF8.GetBytes(value);
            var encoded = HPackHuffman.Encode(input);

            var isShorter = encoded.Length < input.Length;
            Assert.That(isShorter, Is.EqualTo(expectedShorter), $"For '{value}': huffman={encoded.Length}, raw={input.Length}, shorter={isShorter}, expected={expectedShorter}");
        }
    }

    [Test]
    public void GetEncodedLength_Returns_CorrectByteCount()
    {
        var input = "text/html"u8.ToArray();
        var encoded = HPackHuffman.Encode(input);

        var calculatedLength = HPackHuffman.GetEncodedLength(input);

        Assert.That(calculatedLength, Is.EqualTo(encoded.Length));
    }

    [Test]
    public void Padding_Matches_RFC_Requirements()
    {
        var input = "test"u8.ToArray();
        var encoded = HPackHuffman.Encode(input);

        var decoded = HPackHuffman.Decode(encoded);

        Assert.That(decoded, Is.EqualTo(input));
    }
}
