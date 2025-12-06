using Arlirad.Http3.Enums;
using Arlirad.Http3.Streams;

namespace Arlirad.Http3.Framing;

internal class Http3Frame(FrameType type, Stream stream, long length)
{
    public FrameType Type { get => type; }
    public ClampedStream Stream { get; } = new(stream, length);
}