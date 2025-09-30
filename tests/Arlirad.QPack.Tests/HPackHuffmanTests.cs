using System.Text;
using Arlirad.QPack.Huffman;

namespace Arlirad.QPack.Tests;

public class HPackHuffmanTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void HuffmanDecodeTest()
    {
        var data = new byte[]
        {
            0xd0, 0x7a, 0xbe, 0x94, 0x10, 0x54, 0xd4, 0x44, 0xa8, 0x20, 0x05, 0x95, 0x04, 0x0b, 0x81, 0x66,
            0xe0, 0x84, 0xa6, 0x2d, 0x1b, 0xff,
        };

        var decoded = Encoding.UTF8.GetString(HPackHuffman.Decode(data));
        Assert.That(decoded, Is.EqualTo("Mon, 21 Oct 2013 20:13:22 GMT"));

        var data2 = new byte[]
        {
            0x64, 0x02,
        };

        var decoded2 = HPackHuffman.Decode(data2);
        Assert.That(decoded2, Is.EqualTo("302"));
    }
}