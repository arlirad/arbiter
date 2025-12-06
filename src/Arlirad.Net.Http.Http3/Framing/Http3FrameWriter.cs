using Arlirad.Http3.Enums;
using Arlirad.Http3.Streams;

namespace Arlirad.Http3.Framing;

internal class Http3FrameWriter(Stream stream)
{
    private readonly byte[] _buffer = new byte[8];
    private readonly Http3Writer _reader = new(stream);

    public async ValueTask WriteFrameHeader(FrameType type, ulong length, CancellationToken ct = default)
    {
        await _reader.WriteVarInt((ulong)type, _buffer, ct);
        await _reader.WriteVarInt(length, _buffer, ct);
    }

    public async ValueTask WriteFrame(FrameType type, Stream payload, CancellationToken ct = default)
    {
        await WriteFrameHeader(type, (ulong)(payload.Length - payload.Position), ct);
        await payload.CopyToAsync(stream, ct);
    }
}