using System.Buffers;

namespace Arbiter.Protocol.WebSocket;

public class WebSocketFrameReader(Stream stream)
{
    public async Task<WebSocketFrame> ReadFrame(CancellationToken ct = default)
    {
        var header = ArrayPool<byte>.Shared.Rent(2);
        byte[]? ext = null;
        byte[]? mask = null;

        try
        {
            await ReadExact(header.AsMemory(0, 2), ct);

            var fin = (header[0] & 0x80) != 0;
            var opcode = (WebSocketOpcode)(header[0] & 0x0F);
            var masked = (header[1] & 0x80) != 0;
            var payloadLen = (ulong)(header[1] & 0x7F);

            if (payloadLen == 126)
            {
                ext = ArrayPool<byte>.Shared.Rent(2);
                await ReadExact(ext.AsMemory(0, 2), ct);
                payloadLen = (ulong)((ext[0] << 8) | ext[1]);
            }
            else if (payloadLen == 127)
            {
                ext = ArrayPool<byte>.Shared.Rent(8);
                await ReadExact(ext.AsMemory(0, 8), ct);
                payloadLen = 0;

                for (var i = 0; i < 8; i++)
                    payloadLen = (payloadLen << 8) | ext[i];
            }

            if (masked)
            {
                mask = ArrayPool<byte>.Shared.Rent(4);
                await ReadExact(mask.AsMemory(0, 4), ct);
            }

            var payload = ArrayPool<byte>.Shared.Rent((int)payloadLen);

            if (payloadLen > 0)
                await ReadExact(payload.AsMemory(0, (int)payloadLen), ct);

            if (mask is not null)
            {
                for (var i = 0; i < (int)payloadLen; i++)
                    payload[i] ^= mask[i % 4];
            }

            return new WebSocketFrame(opcode, fin, payload.AsMemory(0, (int)payloadLen));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);

            if (ext is not null)
                ArrayPool<byte>.Shared.Return(ext);

            if (mask is not null)
                ArrayPool<byte>.Shared.Return(mask);
        }
    }

    private async Task ReadExact(Memory<byte> buffer, CancellationToken ct)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], ct);

            if (read == 0)
                throw new EndOfStreamException();

            offset += read;
        }
    }
}
