using System.Text;
using Arlirad.QPack.Common;
using Arlirad.QPack.Huffman;

namespace Arlirad.QPack.Streams;

public class QPackReader(Stream inner)
{
    public ulong ReadPrefixedInt(int prefix) => ReadPrefixedInt(prefix, out _);

    public ulong ReadPrefixedInt(int prefix, out byte firstByte)
    {
        var shiftAmount = 8 - prefix;
        var firstByteInt = inner.ReadByte();

        if (firstByteInt == -1)
            throw new EndOfStreamException();

        firstByte = (byte)firstByteInt;
        firstByteInt &= (0xFF >> shiftAmount);

        return firstByteInt < (1 << prefix) - 1
            ? (ulong)firstByteInt
            : (ulong)firstByteInt + ReadPrefixedIntVariablePart();
    }

    public ulong ReadPrefixedIntFromProvidedByte(int prefix, in int firstByte)
    {
        var shiftAmount = 8 - prefix;
        var firstByteInt = firstByte & (0xFF >> shiftAmount);

        if (firstByteInt < (1 << prefix) - 1)
            return (ulong)firstByteInt;

        return (ulong)firstByteInt + ReadPrefixedIntVariablePart();
    }

    public async ValueTask<(ulong Value, byte FirstByte)> ReadPrefixedIntAsync(
        int prefix,
        byte[] buffer,
        CancellationToken ct = default
    )
    {
        var memory = new Memory<byte>(buffer);
        await inner.ReadExactlyAsync(memory, ct);

        var firstByte = buffer[0];

        var shiftAmount = 8 - prefix;
        var firstByteInt = (int)firstByte;

        firstByteInt &= (0xFF >> shiftAmount);

        return firstByteInt < (1 << prefix) - 1
            ? ((ulong)firstByteInt, firstByte)
            : ((ulong)firstByteInt + await ReadPrefixedIntVariablePartAsync(buffer, ct), firstByte);
    }

    public async ValueTask<ulong> ReadPrefixedIntFromProvidedByteAsync(
        int prefix,
        byte firstByte,
        byte[] buffer,
        CancellationToken ct = default
    )
    {
        var shiftAmount = 8 - prefix;
        var firstByteInt = (int)firstByte;

        firstByteInt &= (0xFF >> shiftAmount);

        return firstByteInt < (1 << prefix) - 1
            ? (ulong)firstByteInt
            : (ulong)firstByteInt + await ReadPrefixedIntVariablePartAsync(buffer, ct);
    }

    public string ReadString()
    {
        var length = (int)ReadPrefixedInt(7, out var firstByte);
        var isHuffman = (firstByte & QPackConsts.HuffmanStringMask) == QPackConsts.HuffmanStringMask;

        if (length > inner.Length - inner.Position)
            throw new EndOfStreamException();

        var buffer = new byte[length];
        inner.ReadExactly(buffer, 0, length);

        return Encoding.UTF8.GetString(isHuffman
            ? HPackHuffman.Decode(buffer)
            : buffer);
    }

    public async ValueTask<string> ReadStringAsync(byte[] varIntBuffer, CancellationToken ct)
    {
        var (length, firstByte) = await ReadPrefixedIntAsync(7, varIntBuffer, ct);
        var isHuffman = (firstByte & QPackConsts.HuffmanStringMask) == QPackConsts.HuffmanStringMask;
        var buffer = new byte[length];

        await inner.ReadExactlyAsync(buffer, 0, (int)length, ct);

        return Encoding.UTF8.GetString(isHuffman
            ? HPackHuffman.Decode(buffer)
            : buffer);
    }

    public async ValueTask<string> ReadStringAsync(
        byte[] varIntBuffer,
        int prefix,
        byte firstByte,
        int huffmanBit,
        CancellationToken ct)
    {
        var mask = 1 << huffmanBit;
        var length = await ReadPrefixedIntFromProvidedByteAsync(prefix, firstByte, varIntBuffer, ct);
        var isHuffman = (firstByte & mask) == mask;
        var buffer = new byte[length];

        await inner.ReadExactlyAsync(buffer, 0, (int)length, ct);

        return Encoding.UTF8.GetString(isHuffman
            ? HPackHuffman.Decode(buffer)
            : buffer);
    }

    private ulong ReadPrefixedIntVariablePart()
    {
        var result = 0ul;
        var nextShiftAmount = 0;
        int nextByte;

        do
        {
            nextByte = inner.ReadByte();

            if (nextByte == -1)
                throw new EndOfStreamException();

            result += ((ulong)nextByte & 0b0111_1111) << nextShiftAmount;
            nextShiftAmount += 7;
        } while ((nextByte & 0b1000_0000) == 0b1000_0000);

        return result;
    }

    private async ValueTask<ulong> ReadPrefixedIntVariablePartAsync(byte[] buffer, CancellationToken ct)
    {
        var memory = new Memory<byte>(buffer);
        var result = 0ul;
        var nextShiftAmount = 0;

        do
        {
            await inner.ReadExactlyAsync(memory, ct);

            result += ((ulong)buffer[0] & 0b0111_1111) << nextShiftAmount;
            nextShiftAmount += 7;
        } while ((buffer[0] & 0b1000_0000) == 0b1000_0000);

        return result;
    }
}