using Arlirad.Http3.Enums;
using Arlirad.Http3.Streams;

namespace Arlirad.Http3.Framing;

internal class Http3FrameReader(Stream stream)
{
    private readonly byte[] _buffer = new byte[8];
    private readonly Http3Reader _reader = new(stream);

    public async ValueTask<Http3Frame> ReadFrame(CancellationToken ct = default)
    {
        var type = await _reader.ReadVarInt(_buffer, ct);
        var length = await _reader.ReadVarInt(_buffer, ct);

        return new Http3Frame((FrameType)type, stream, length);
    }
}