using Arbiter.Application.Interfaces;

namespace Arbiter.Application;

public sealed class TransportStream(Stream stream, long streamId) : ITransportStream
{
    public Stream Stream => stream;
    public long StreamId => streamId;
}
