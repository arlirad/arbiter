using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Arbiter.Application.Interfaces;

namespace Arbiter.Protocol.Http3.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IMultiplexedConnection"/> for unit-testing the HTTP/3 layer without real QUIC.
/// Inbound streams arrive via a channel (test-enqueueable); outbound streams are produced on demand.
/// </summary>
public sealed class FakeMultiplexedConnection : IMultiplexedConnection
{
    private readonly Channel<ITransportStream> _inbound = Channel.CreateUnbounded<ITransportStream>();
    private long _nextStreamId;

    public Core.Enums.Protocol Protocol => Core.Enums.Protocol.Http3;
    public bool IsSecure => true;
    public int Port => 0;
    public IPAddress? RemoteAddress => null;

    // Test hook: enqueue an inbound stream that GetStreams will yield.
    public void EnqueueInbound(FakeMultiplexedStream stream) => _inbound.Writer.TryWrite(stream);

    public async IAsyncEnumerable<ITransportStream> GetStreams([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var stream in _inbound.Reader.ReadAllAsync(ct))
            yield return stream;
    }

    public Task<IMultiplexedStream> OpenStreamAsync(MultiplexedStreamDirection direction, CancellationToken ct = default)
    {
        var stream = new FakeMultiplexedStream(Interlocked.Increment(ref _nextStreamId), direction);
        return Task.FromResult<IMultiplexedStream>(stream);
    }

    public ValueTask CloseAsync(long errorCode, CancellationToken ct = default)
    {
        _inbound.Writer.TryComplete();
        return default;
    }

    public Task<IConnection> UpgradeAsync(Core.Enums.Protocol targetProtocol)
        => throw new NotSupportedException("Fake connection cannot be upgraded.");

    public ValueTask DisposeAsync()
    {
        _inbound.Writer.TryComplete();
        return default;
    }
}

/// <summary>
/// In-memory <see cref="IMultiplexedStream"/> backed by a <see cref="MemoryStream"/>.
/// </summary>
public sealed class FakeMultiplexedStream(long streamId, MultiplexedStreamDirection direction) : IMultiplexedStream
{
    public Stream Stream => _stream;
    public long StreamId => streamId;
    public MultiplexedStreamDirection Direction => direction;

    public ValueTask CompleteWritesAsync(CancellationToken ct = default) => default;

    public ValueTask AbortAsync(long errorCode, CancellationToken ct = default) => default;

    private readonly MemoryStream _stream = new();
}
