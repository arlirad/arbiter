namespace Arbiter.Infrastructure.Streams;

public class RemainderStream(Stream inner, Stream? remainder = null) : Stream
{
    public override bool CanRead { get => true; }
    public override bool CanSeek { get => true; }
    public override bool CanWrite { get => false; }
    public override long Length { get => throw new NotSupportedException(); }
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var actualReadLength = 0;

        if (remainder is not null && remainder.Position != remainder.Length)
        {
            var maxReadLength = Math.Min(remainder.Length - remainder.Position, buffer.Length);
            var remainderReadLength = (int)Math.Max(0, Math.Min(int.MaxValue, maxReadLength));

            if (remainderReadLength == 0)
                return actualReadLength;

            var remainderBuffer = buffer[..remainderReadLength];

            buffer = buffer[remainderReadLength..];

            actualReadLength += await remainder.ReadAsync(remainderBuffer, cancellationToken);
        }

        if (buffer.Length > 0)
            actualReadLength += await inner.ReadAsync(buffer, cancellationToken);

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