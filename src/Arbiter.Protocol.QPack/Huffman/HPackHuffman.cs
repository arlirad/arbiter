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
        return (padding == (0xFF >> (8 - remaining)))
            ? ms.ToArray()
            : throw new Exception("Huffman decoding error");
    }

    public static byte[] Encode(byte[] input)
    {
        if (input.Length == 0)
            return [];

        var totalBits = 0;
        foreach (var b in input)
            totalBits += HuffmanEncodeTable.Table[b].BitLength;

        var writer = new BitWriter(totalBits / 8 + 2);

        foreach (var b in input)
        {
            var (code, bitLength) = HuffmanEncodeTable.Table[b];
            writer.WriteBits(code, bitLength);
        }

        // Pad with EOS (End of String) symbol bits: 0x3FFFFFFF (30 bits)
        var writtenBits = writer.BitLength;
        var paddingNeeded = (8 - (writtenBits % 8)) % 8;

        if (paddingNeeded > 7)
            throw new InvalidOperationException("Huffman encoding error: padding would exceed 7 bits");

        if (paddingNeeded > 0)
        {
            // The EOS symbol code is 0x3FFFFFFF (30 bits). Take the top 'paddingNeeded' bits.
            var eosCode = 0x3FFFFFFF;
            var paddingBits = eosCode >> (30 - paddingNeeded);
            writer.WriteBits((uint)paddingBits, paddingNeeded);
        }

        return writer.ToArray();
    }

    public static int GetEncodedLength(byte[] input)
    {
        if (input.Length == 0)
            return 0;

        var totalBits = 0;
        foreach (var b in input)
            totalBits += HuffmanEncodeTable.Table[b].BitLength;

        var byteLength = (totalBits + 7) / 8;
        return byteLength;
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