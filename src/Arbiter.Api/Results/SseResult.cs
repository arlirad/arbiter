using System.Threading.Channels;
using Arbiter.Api.Http;

namespace Arbiter.Api.Results;

public class SseResult(IAsyncEnumerable<SseEvent> events, CancellationToken cancellationToken = default) : IActionResult
{
    public SseResult(Func<SseWriter, Task> writerFunc)
        : this(CreateEnumerableFromWriter(writerFunc), CancellationToken.None)
    {
    }

    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(evt.Event))
                await context.Response.WriteAsync($"event: {evt.Event}\n");

            if (!string.IsNullOrEmpty(evt.Data))
            {
                foreach (var line in evt.Data.Split('\n'))
                    await context.Response.WriteAsync($"data: {line}\n");
            }

            if (evt.Id is not null)
                await context.Response.WriteAsync($"id: {evt.Id}\n");

            if (evt.Retry is { } retry)
                await context.Response.WriteAsync($"retry: {retry}\n");

            await context.Response.WriteAsync("\n");
        }
    }

    private static IAsyncEnumerable<SseEvent> CreateEnumerableFromWriter(Func<SseWriter, Task> writerFunc) => new WriterEnumerable(writerFunc);

    private sealed class WriterEnumerable(Func<SseWriter, Task> writerFunc) : IAsyncEnumerable<SseEvent>
    {
        public IAsyncEnumerator<SseEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new WriterEnumerator(writerFunc, cancellationToken);
    }

    private sealed class WriterEnumerator : IAsyncEnumerator<SseEvent>
    {
        private readonly CancellationToken _cancellationToken;
        private readonly Channel<SseEvent> _channel = Channel.CreateUnbounded<SseEvent>();
        private readonly Task _task;
        private SseEvent? _current;

        public WriterEnumerator(Func<SseWriter, Task> writerFunc, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            var writer = new SseWriter(null!);

            _task = Task.Run(async () => {
                await writerFunc(writer);
                _channel.Writer.Complete();
            }, cancellationToken);
        }

        public SseEvent Current => _current ?? throw new InvalidOperationException();

        public ValueTask<bool> MoveNextAsync()
        {
            if (_current is null)
                return _channel.Reader.WaitToReadAsync(_cancellationToken);

            _current = null;

            return new ValueTask<bool>(true);
        }

        public ValueTask DisposeAsync()
        {
            _channel.Writer.Complete();

            return default;
        }
    }
}
