namespace Arbiter.Infrastructure.Proxy;

internal sealed class ResponseStream(Stream stream, HttpResponseMessage response) : Stream
{
    public override bool CanRead { get => stream.CanRead; }
    public override bool CanSeek { get => stream.CanSeek; }
    public override bool CanWrite { get => stream.CanWrite; }
    public override long Length { get => stream.Length; }
    public override long Position { get => stream.Position; set => stream.Position = value; }

    public override int Read(byte[] buffer, int offset, int count) =>
        stream.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
        stream.ReadAsync(buffer, ct);

    public override long Seek(long offset, SeekOrigin origin) =>
        stream.Seek(offset, origin);

    public override void SetLength(long value) =>
        stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        stream.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) =>
        stream.WriteAsync(buffer, ct);

    public override void Flush() =>
        stream.Flush();

    public override Task FlushAsync(CancellationToken ct) =>
        stream.FlushAsync(ct);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            stream.Dispose();
            response.Dispose();
        }

        base.Dispose(disposing);
    }

    public async override ValueTask DisposeAsync()
    {
        await stream.DisposeAsync();
        response.Dispose();
        await base.DisposeAsync();
    }
}