namespace Arbiter.Application.Interfaces;

/// <summary>
/// A logical stream multiplexed over an <see cref="IMultiplexedConnection"/>.
/// Adds direction metadata and stream-level lifecycle operations on top of <see cref="ITransportStream"/>.
/// </summary>
public interface IMultiplexedStream : ITransportStream
{
    /// <summary>Whether this stream is unidirectional or bidirectional.</summary>
    MultiplexedStreamDirection Direction { get; }

    /// <summary>Half-close the write side of the stream (no more data will be written).</summary>
    ValueTask CompleteWritesAsync(CancellationToken ct = default);

    /// <summary>Reset the stream with the given application error code.</summary>
    ValueTask AbortAsync(long errorCode, CancellationToken ct = default);
}
