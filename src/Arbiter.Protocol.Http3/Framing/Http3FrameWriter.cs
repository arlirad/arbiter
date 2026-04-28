using Arlirad.Http3.Enums;
using Arlirad.Http3.Streams;

namespace Arlirad.Http3.Framing;

internal class Http3FrameWriter(Stream stream)
{
    private readonly byte[] _buffer = new byte[16];

    public async ValueTask WriteFrameHeader(FrameType type, ulong length, CancellationToken ct = default)
    {
        var writer = new Http3Writer(stream);
        var offset = 0;

        WriteVarIntSync((ulong)type, _buffer, ref offset);
        WriteVarIntSync(length, _buffer, ref offset);

        await stream.WriteAsync(new ReadOnlyMemory<byte>(_buffer, 0, offset), ct);
    }

    public async ValueTask WriteFrame(FrameType type, Stream payload, CancellationToken ct = default)
    {
        await WriteFrameHeader(type, (ulong)(payload.Length - payload.Position), ct);
        await payload.CopyToAsync(stream, ct);
    }

    private static void WriteVarIntSync(ulong value, byte[] buffer, ref int offset)
    {
        var length = value switch {
            <= 63 => 1,
            <= 16383 => 2,
            <= 1073741823 => 4,
            _ => 8,
        };
        var prefix = length switch {
            1 => 0b0000_0000,
            2 => 0b0100_0000,
            4 => 0b1000_0000,
            _ => 0b1100_0000,
        };
        var i = offset + length - 1;

        while (i > offset)
        {
            buffer[i--] = (byte)(value & 0xFF);
            value >>= 8;
        }

        buffer[offset] = (byte)(prefix | (byte)(value & 0x3F));
        offset += length;
    }
}