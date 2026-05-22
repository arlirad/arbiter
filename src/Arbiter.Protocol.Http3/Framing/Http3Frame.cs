using Arbiter.Infrastructure.Streams;
using Arbiter.Protocol.Http3.Enums;

namespace Arbiter.Protocol.Http3.Framing;

internal class Http3Frame(FrameType type, Stream stream, long length)
{
    public FrameType Type => type;
    public ClampedStream Stream
    {
        get;
    } = new(stream, length);
}
