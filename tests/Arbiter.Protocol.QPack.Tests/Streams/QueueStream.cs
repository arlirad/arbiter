namespace Arbiter.Protocol.QPack.Tests.Streams;

public class QueueStream : Stream
{
    private readonly Lock _lock = new();
    private readonly Queue<byte> _queue = new();
    private volatile TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => _queue.Count;
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();

    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task? toAwait;

            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    var read = 0;

                    while (read < buffer.Length && _queue.Count > 0)
                    {
                        buffer.Span[read++] = _queue.Dequeue();
                    }

                    if (_queue.Count == 0 && _tcs.Task.IsCompleted)
                        _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                    return read;
                }

                toAwait = _tcs.Task;
            }

            await toAwait.WaitAsync(cancellationToken);
        }
    }

    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            for (var i = 0; i < buffer.Length; i++)
                _queue.Enqueue(buffer.Span[i]);
        }

        _tcs.TrySetResult();

        return ValueTask.CompletedTask;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
