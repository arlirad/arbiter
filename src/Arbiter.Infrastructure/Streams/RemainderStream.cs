namespace Arbiter.Infrastructure.Streams;

public class RemainderStream(Stream inner, Stream? remainder = null) : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (remainder is not null && remainder.Position != remainder.Length)
        {
            var maxReadLength = Math.Min(remainder.Length - remainder.Position, buffer.Length);
            var remainderReadLength = (int)Math.Max(0, Math.Min(int.MaxValue, maxReadLength));

            if (remainderReadLength > 0)
                return await remainder.ReadAsync(buffer[..remainderReadLength], cancellationToken);
        }

        return await inner.ReadAsync(buffer, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => inner.WriteAsync(buffer, cancellationToken);

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            remainder?.Dispose();
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
