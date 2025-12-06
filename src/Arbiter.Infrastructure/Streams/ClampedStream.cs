namespace Arbiter.Infrastructure.Streams;

public class ClampedStream(Stream inner, long length) : Stream
{
    private long _position;
    public override bool CanRead { get => true; }
    public override bool CanSeek { get => true; }
    public override bool CanWrite { get => false; }
    public override long Length { get => length; }
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if ((uint)offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        if (count < 0 || count > buffer.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(count));

        var remainingLength = (int)Math.Max(0, Math.Min(int.MaxValue, Length - Position));
        var actualReadLength = inner.Read(buffer, offset, Math.Min(remainingLength, count));

        _position += actualReadLength;

        return remainingLength != 0 ? actualReadLength : 0;
    }

    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var remainingLength = (int)Math.Max(0, Math.Min(int.MaxValue, Length - Position));

        if (remainingLength == 0)
            return 0;

        var actualReadLength =
            await inner.ReadAsync(buffer[..Math.Min(remainingLength, buffer.Length)], cancellationToken);

        _position += actualReadLength;

        return actualReadLength;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
        inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return inner.FlushAsync(cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}