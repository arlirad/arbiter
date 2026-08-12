namespace Arbiter.Application.Interfaces;

/// <summary>
/// A connection capable of carrying multiple logical streams (HTTP/3 over QUIC today; HTTP/2 later).
/// </summary>
public interface IMultiplexedConnection : IConnection
{
    /// <summary>Open a new server-initiated stream with the given direction.</summary>
    Task<IMultiplexedStream> OpenStreamAsync(MultiplexedStreamDirection direction, CancellationToken ct = default);

    /// <summary>
    /// Close the connection with the given application/transport error code.
    /// NOTE: currently exercised only by the HTTP/3 layer. HTTP/2's connection-close story
    /// (GOAWAY frame + TCP close) is TBD and may result in this member moving or being reshaped.
    /// </summary>
    ValueTask CloseAsync(long errorCode, CancellationToken ct = default);

    // NOTE: GetStreams is inherited unchanged from IConnection. It still returns
    // IAsyncEnumerable<ITransportStream>; implementations yield IMultiplexedStream instances
    // (which are ITransportStream by inheritance). Consumers pattern-match to recover the
    // stronger type:  `if (ts is IMultiplexedStream ms) { ... }`.
}
