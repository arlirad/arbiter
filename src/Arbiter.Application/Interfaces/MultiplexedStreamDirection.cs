namespace Arbiter.Application.Interfaces;

/// <summary>
/// Direction of a logical stream multiplexed over an <see cref="IMultiplexedConnection"/>.
/// HTTP/3 distinguishes unidirectional (control/encoder/decoder) from bidirectional (request) streams.
/// HTTP/2 streams are always bidirectional.
/// </summary>
public enum MultiplexedStreamDirection
{
    Unidirectional,
    Bidirectional,
}
