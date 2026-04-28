using Arlirad.Http3.Enums;
using Arlirad.Http3.Streams;

namespace Arlirad.Http3.Framing;

internal class Http3FrameReader(Stream stream)
{
    private readonly byte[] _buffer = new byte[16];

    public async ValueTask<Http3Frame> ReadFrame(CancellationToken ct = default)
    {
        await stream.ReadExactlyAsync(new Memory<byte>(_buffer, 0, 1), ct);

        var firstValue = (long)_buffer[0];
        var prefix = (int)(firstValue >> 6);
        var varIntLength = 1 << prefix;
        firstValue &= 0x3F;

        if (varIntLength > 1)
            await stream.ReadExactlyAsync(new Memory<byte>(_buffer, 1, varIntLength - 1), ct);

        var type = firstValue;
        for (var i = 1; i < varIntLength; i++)
            type = (type << 8) + _buffer[i];

        await stream.ReadExactlyAsync(new Memory<byte>(_buffer, 0, 1), ct);

        var secondValue = (long)_buffer[0];
        prefix = (int)(secondValue >> 6);
        varIntLength = 1 << prefix;
        secondValue &= 0x3F;

        if (varIntLength > 1)
            await stream.ReadExactlyAsync(new Memory<byte>(_buffer, 1, varIntLength - 1), ct);

        var length = secondValue;
        for (var i = 1; i < varIntLength; i++)
            length = (length << 8) + _buffer[i];

        return new Http3Frame((FrameType)type, stream, length);
    }
}