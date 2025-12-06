namespace Arlirad.QPack.Tests.Streams;

public class QueueStream : Stream
{
    private readonly Queue<byte> _queue = new();
    private volatile TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override bool CanRead { get => true; }
    public override bool CanSeek { get => true; }
    public override bool CanWrite { get => true; }
    public override long Length { get => _queue.Count; }
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
    }

    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_queue.Count == 0)
            await Wait().WaitAsync(cancellationToken);

        var read = 0;

        while (read < buffer.Length && _queue.Count > 0)
        {
            buffer.Span[read] = _queue.Dequeue();
            read++;
        }

        if (_queue.Count == 0)
            Reset();

        return read;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            _queue.Enqueue(buffer.Span[i]);
        }

        Set();

        return ValueTask.CompletedTask;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    private Task Wait() => _tcs.Task;

    private void Set() => _tcs.TrySetResult();

    private void Reset()
    {
        if (_tcs.Task.IsCompleted)
            _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}