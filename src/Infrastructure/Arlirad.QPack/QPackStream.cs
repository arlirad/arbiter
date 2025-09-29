namespace Arlirad.QPack;

public class QPackStream(Stream inner) : Stream
{
    public override bool CanRead { get; }
    public override bool CanSeek { get; }
    public override bool CanWrite { get; }
    public override long Length { get; }
    public override long Position { get; set; }

    public ulong ReadVarInt(int prefix)
    {
        var shiftAmount = 8 - prefix;
        var firstByte = ReadByte() & (0xFF >> shiftAmount);

        if (firstByte == -1)
            throw new EndOfStreamException();

        if (firstByte < (1 << prefix) - 1)
            return (ulong)firstByte;

        var result = (ulong)firstByte;
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

    public override void Flush() => inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

    public override void SetLength(long value) => inner.SetLength(value);
}