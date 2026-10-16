using System.Net.Quic;
using System.Runtime.Versioning;
using Arbiter.Application.Interfaces;

namespace Arbiter.Transport.Quic;

/// <summary>
/// <see cref="IMultiplexedStream"/> implementation backed by a BCL <see cref="QuicStream"/>.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public sealed class QuicMultiplexedStream(QuicStream stream) : IMultiplexedStream
{
    public Stream Stream => stream;

    public long StreamId => stream.Id;

    public MultiplexedStreamDirection Direction
        => stream.Type == QuicStreamType.Unidirectional
            ? MultiplexedStreamDirection.Unidirectional
            : MultiplexedStreamDirection.Bidirectional;

    public ValueTask CompleteWritesAsync(CancellationToken ct = default)
    {
        stream.CompleteWrites();
        return default;
    }

    public ValueTask AbortAsync(long errorCode, CancellationToken ct = default)
    {
        stream.Abort(QuicAbortDirection.Write, errorCode);
        return default;
    }
}
