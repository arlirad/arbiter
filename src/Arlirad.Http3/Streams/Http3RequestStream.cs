using System.Net.Quic;
using System.Runtime.CompilerServices;
using Arlirad.Http3.Enums;
using Arlirad.Http3.Framing;

namespace Arlirad.Http3.Streams;

#pragma warning disable CA1416

public class Http3RequestStream(Http3Connection connection, long streamId, QuicStream inner) : Stream
{
    private readonly Http3FrameReader _reader = new(inner);
    private readonly Http3FrameWriter _writer = new(inner);
    private Http3Frame? _currentDataFrame;
    public override bool CanRead { get => true; }
    public override bool CanSeek { get => false; }
    public override bool CanWrite { get => true; }
    public override long Length { get => throw new NotSupportedException(); }
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public async IAsyncEnumerable<KeyValuePair<string, string?>> ReadHeaders(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var headersFrame = await _reader.ReadFrame(ct);
        var reader = await connection.Decoder.GetSectionReader(streamId, headersFrame.Stream, ct);

        foreach (var field in reader)
        {
            yield return new KeyValuePair<string, string?>(field.Name, field.Value);
        }
    }

    public async ValueTask WriteHeaders(
        IEnumerable<KeyValuePair<string, string>> headers,
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        var writer = await connection.Encoder.GetSectionWriter(streamId, ms, ct);

        await writer.WritePrefix(ct);

        foreach (var header in headers)
        {
            await writer.Write(header.Key, header.Value, ct);
        }

        await _writer.WriteFrameHeader(FrameType.Headers, (ulong)ms.Length, ct);
        await inner.WriteAsync(ms.ToArray(), ct);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Synchronous reads are not supported");
    }

    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_currentDataFrame is null || _currentDataFrame.Stream.Position == _currentDataFrame.Stream.Length)
            _currentDataFrame = await _reader.ReadFrame(ct);

        return await _currentDataFrame.Stream.ReadAsync(buffer, ct);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        await _writer.WriteFrameHeader(FrameType.Data, (ulong)buffer.Length, ct);
        await inner.WriteAsync(buffer, ct);
    }

    public override void Flush()
    {
        throw new NotSupportedException();
    }

    public void Finish()
    {
        inner.CompleteWrites();
    }
}