namespace Arlirad.Infrastructure.QPack.Streams;

public class BitWriter
{
    private byte[] _buffer;
    private int _bitPosition;
    private int _totalBits;

    public BitWriter(int initialCapacity)
    {
        _buffer = new byte[initialCapacity];
    }


    public byte[] ToArray()
    {
        var result = new byte[(_totalBits + 7) / 8];
        Array.Copy(_buffer, result, result.Length);
        return result;
    }

    public int BitLength => _totalBits;

    public int TotalBytes => (_totalBits + 7) / 8;

    public void WriteBits(uint value, int bitCount)
    {
        if (bitCount is not (> 0 and <= 32))
            throw new ArgumentOutOfRangeException(nameof(bitCount), "bitCount must be between 1 and 32");

        var valueMasked = value & ((1u << bitCount) - 1);

        while (bitCount > 0)
        {
            EnsureCapacity();

            var bitsInCurrentByte = 8 - (_bitPosition % 8);
            var bitsToWrite = Math.Min(bitCount, bitsInCurrentByte);
            var currentByteIndex = _bitPosition / 8;

            var shift = bitsInCurrentByte - bitsToWrite;
            var bitsToWriteMasked = (byte)(valueMasked >> (bitCount - bitsToWrite));
            bitsToWriteMasked &= (byte)((1 << bitsToWrite) - 1);

            _buffer[currentByteIndex] |= (byte)(bitsToWriteMasked << shift);

            _bitPosition += bitsToWrite;
            bitCount -= bitsToWrite;
            valueMasked &= (1u << bitCount) - 1;
        }

        _totalBits += _bitPosition - _totalBits;
    }

    public void Write(byte value) => WriteBits(value, 8);

    public void Write(ReadOnlySpan<byte> buffer)
    {
        foreach (var b in buffer)
            WriteBits(b, 8);
    }

    private void EnsureCapacity()
    {
        var requiredBytes = (_bitPosition / 8) + 1;
        if (requiredBytes > _buffer.Length)
        {
            var newCapacity = _buffer.Length * 2;
            while (newCapacity < requiredBytes)
                newCapacity *= 2;

            Array.Resize(ref _buffer, newCapacity);
        }
    }
}
