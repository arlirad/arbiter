namespace Arlirad.QPack.Streams;

public class BitStream(byte[] buffer)
{
    private const int ByteSize = sizeof(byte) * 8;

    public int Position { get; set; } = 0;
    public int Length { get; set; } = buffer.Length * ByteSize;

    public long ReadNotAdvancing(int n)
    {
        var pos = Position;
        var (result, available) = ReadPartial(n, pos);

        pos += available;
        n -= available;

        while (n > ByteSize)
        {
            result |= (long)buffer[pos / ByteSize] << (n - ByteSize);
            pos += ByteSize;
            n -= ByteSize;
        }

        if (n <= 0)
            return result;

        var (resultLast, _) = ReadPartial(n, pos);
        result |= resultLast;

        return result;
    }

    private (long, int) ReadPartial(int n, int pos)
    {
        var available = ByteSize - pos % ByteSize;
        var firstShift = Math.Max(available - n, 0);
        var firstByte = buffer[pos / ByteSize] >> firstShift;
        var mask = (1 << Math.Min(available, n)) - 1;
        var result = (long)(firstByte & mask) << Math.Max(n - available, 0);

        return (result, available);
    }
}