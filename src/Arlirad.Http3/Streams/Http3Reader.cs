namespace Arlirad.Http3.Streams;

public class Http3Reader(Stream stream)
{
    /// <summary>
    /// Reads a variable-length integer from a stream as defined in the QUIC protocol.
    /// </summary>
    /// <param name="buffer">
    /// A buffer to hold intermediate bytes while reading the variable-length integer.
    /// The size of the buffer must be enough to accommodate the encoded integer.
    /// </param>
    /// <param name="ct">
    /// An optional cancellation token to observe while waiting for the asynchronous operation to complete.
    /// </param>
    /// <returns>
    /// A <c>ValueTask</c> representing the asynchronous operation. The result of the task is the
    /// decoded variable-length integer as a <c>long</c>.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation is canceled via the provided <paramref name="ct"/>.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when an I/O error occurs during the reading process from the stream.
    /// </exception>
    public async ValueTask<long> ReadVarInt(byte[] buffer, CancellationToken ct = default)
    {
        try
        {
            await stream.ReadExactlyAsync(new Memory<byte>(buffer, 0, 1), ct);

            var value = (long)buffer[0];
            var prefix = value >> 6;
            var length = 1 << (int)prefix;

            value &= 0x3F;

            await stream.ReadExactlyAsync(new Memory<byte>(buffer, 0, length - 1), ct);

            for (var i = 0; i < length - 1; i++)
            {
                value = (value << 8) + buffer[i];
            }

            return value;
        }
        catch (TaskCanceledException)
        {
            throw new OperationCanceledException();
        }
    }
}