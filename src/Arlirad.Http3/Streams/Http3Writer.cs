namespace Arlirad.Http3.Streams;

public class Http3Writer(Stream stream)
{
    /// <summary>
    /// Writes a variable-length integer to a stream as defined in the QUIC protocol.
    /// </summary>
    /// <param name="value">The unsigned integer value to be written.</param>
    /// <param name="buffer">
    /// A byte array that acts as a temporary storage buffer for encoding the integer.
    /// Must have sufficient length to accommodate the encoded value (up to 8 bytes).
    /// </param>
    /// <param name="ct">
    /// An optional cancellation token to observe while waiting for the asynchronous operation to complete.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> representing the asynchronous write operation.
    /// </returns>
    public async ValueTask WriteVarInt(ulong value, byte[] buffer, CancellationToken ct = default)
    {
        var length = value switch
        {
            <= 63 => 1,
            <= 16383 => 2,
            <= 1073741823 => 4,
            <= 4611686018427387903 => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
#pragma warning disable CS8509
        var prefix = length switch
        {
            1 => 0b0000_0000,
            2 => 0b0100_0000,
            4 => 0b1000_0000,
            8 => 0b1100_0000,
        };
#pragma warning restore CS8509
        var offset = length - 1;

        while (offset > 0)
        {
            buffer[offset--] = (byte)(value & 0xFF);
            value >>= 8;
        }

        buffer[offset] = (byte)(prefix | (byte)(value & 0x3F));

        await stream.WriteAsync(buffer.AsMemory(0, length), ct);
    }
}