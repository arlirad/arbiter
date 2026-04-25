using Arlirad.Infrastructure.QPack.Streams;

namespace Arlirad.Infrastructure.QPack.Encoding;

public class QPackEncoder
{
    private Stream? _decoderIncoming;
    private QPackReader? _decoderIncomingReader;
    private Stream? _encoderOutgoing;
    private QPackWriter? _encoderOutgoingWriter;

    public ValueTask Start() => ValueTask.CompletedTask;

    public void SetIncomingStream(Stream stream)
    {
        _decoderIncoming = stream;
        _decoderIncomingReader = new QPackReader(_decoderIncoming);
    }

    public void SetOutgoingStream(Stream stream)
    {
        _encoderOutgoing = stream;
        _encoderOutgoingWriter = new QPackWriter(_encoderOutgoing);
    }

    public Task<QPackFieldSectionWriter> GetSectionWriter(
        long streamId,
        Stream stream,
        CancellationToken ct = default) => Task.FromResult(new QPackFieldSectionWriter(streamId, stream, new QPackWriter(stream), this));
}