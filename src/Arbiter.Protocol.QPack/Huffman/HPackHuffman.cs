using Arlirad.Infrastructure.QPack.Common;
using Arlirad.Infrastructure.QPack.Streams;

namespace Arlirad.Infrastructure.QPack.Huffman;

public static class HPackHuffman
{
    public static byte[] Decode(byte[] buffer)
    {
        var bs = new BitStream(buffer);
        var ms = new MemoryStream();

        while (bs.Position < bs.Length)
        {
            var result = Get(bs);
            if (result == -1)
                break;

            if (result == -2)
                throw new Exception("Huffman decoding error");

            ms.WriteByte((byte)result);
        }

        // To satisfy:
        // Padding strictly longer than 7 bits MUST be treated as a decoding error.
        if (bs.Length - bs.Position > 7)
            throw new Exception("Huffman decoding error");

        var remaining = bs.Length - bs.Position;
        if (remaining <= 0)
            return ms.ToArray();

        var padding = bs.ReadNotAdvancing(remaining);

        // To satisfy:
        // Padding not corresponding to the most significant bits of the code for the EOS symbol MUST be treated as a
        // decoding error.
        return padding == 0xFF >> (8 - remaining)
            ? ms.ToArray()
            : throw new Exception("Huffman decoding error");
    }

    private static int Get(BitStream bs)
    {
        foreach (var lenSection in HPackConsts.Code)
        {
            if (bs.Position + lenSection.Key > bs.Length)
                break;

            if (!lenSection.Value.TryGetValue(bs.ReadNotAdvancing(lenSection.Key), out var sym))
                continue;

            bs.Position += lenSection.Key;
            return sym;
        }

        return -1;
    }
}