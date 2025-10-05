namespace Arlirad.QPack.Streams;

public class QPackWriter(Stream inner)
{
    private readonly byte[] _buffer = new byte[16];

    public async ValueTask WritePrefixedInt(long value, int prefix, byte firstByte, CancellationToken ct)
    {
        if (value < ((1 << prefix) - 1))
        {
            _buffer[0] = (byte)(firstByte | (int)value & ((1 << prefix) - 1));
            await inner.WriteAsync(new Memory<byte>(_buffer, 0, 1), ct);

            return;
        }

        _buffer[0] = (byte)(firstByte | (1 << prefix) - 1);

        value -= (1 << prefix) - 1;

        var length = 1;

        while (value > 127)
        {
            _buffer[length] = (byte)((value & 0b0111_1111) | 0b1000_0000);

            value >>= 7;
            length++;
        }

        _buffer[length] = (byte)value;
        length++;

        await inner.WriteAsync(new Memory<byte>(_buffer, 0, length), ct);
    }
}