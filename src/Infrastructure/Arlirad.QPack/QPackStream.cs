using System.Text;

namespace Arlirad.QPack;

public class QPackStream(Stream inner) : Stream
{
    public override bool CanRead { get; } = inner.CanRead;
    public override bool CanSeek { get; } = inner.CanSeek;
    public override bool CanWrite { get; } = inner.CanWrite;
    public override long Length { get; } = inner.Length;
    public override long Position { get; set; } = inner.Position;

    public ulong ReadVarInt(int prefix) => ReadVarInt(prefix, out _);

    public ulong ReadVarInt(int prefix, out byte firstByte)
    {

        var shiftAmount = 8 - prefix;
        var firstByteInt = ReadByte();

        if (firstByteInt == -1)
            throw new EndOfStreamException();

        firstByte = (byte)firstByteInt;
        firstByteInt &= (0xFF >> shiftAmount);

        return firstByteInt < (1 << prefix) - 1
            ? (ulong)firstByteInt
            : (ulong)firstByteInt + ReadVarIntVariablePart();

    }

    public ulong ReadVarIntFromProvidedByte(int prefix, in int firstByte)
    {
        var shiftAmount = 8 - prefix;
        var firstByteInt = firstByte & (0xFF >> shiftAmount);

        if (firstByteInt < (1 << prefix) - 1)
            return (ulong)firstByteInt;

        return (ulong)firstByteInt + ReadVarIntVariablePart();
    }

    public string ReadString()
    {
        var length = (int)ReadVarInt(7, out var firstByte);
        var isHuffman = (firstByte & QPackConsts.HuffmanStringMask) == QPackConsts.HuffmanStringMask;

        if (length > Length - Position)
            throw new EndOfStreamException();

        var buffer = new byte[length];
        inner.ReadExactly(buffer, 0, length);

        return Encoding.UTF8.GetString(isHuffman
            ? HPackHuffman.Decode(buffer)
            : buffer);
    }

    public override void Flush() => inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void SetLength(long value) => inner.SetLength(value);

    private ulong ReadVarIntVariablePart()
    {
        var result = 0ul;
        var nextShiftAmount = 0;
        int nextByte;

        do
        {
            nextByte = ReadByte();

            if (nextByte == -1)
                throw new EndOfStreamException();

            result += ((ulong)nextByte & 0b0111_1111) << nextShiftAmount;
            nextShiftAmount += 7;
        } while ((nextByte & 0b1000_0000) == 0b1000_0000);

        return result;
    }
}