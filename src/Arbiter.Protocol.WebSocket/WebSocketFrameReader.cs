namespace Arlirad.WebSocket;

public class WebSocketFrameReader(Stream stream)
{
    public async Task<WebSocketFrame> ReadFrame(CancellationToken ct = default)
    {
        var header = new byte[2];
        await ReadExact(header, ct);

        var fin = (header[0] & 0x80) != 0;
        var opcode = (WebSocketOpcode)(header[0] & 0x0F);
        var masked = (header[1] & 0x80) != 0;
        var payloadLen = (ulong)(header[1] & 0x7F);

        if (payloadLen == 126)
        {
            var ext = new byte[2];
            await ReadExact(ext, ct);
            payloadLen = (ulong)((ext[0] << 8) | ext[1]);
        }
        else if (payloadLen == 127)
        {
            var ext = new byte[8];
            await ReadExact(ext, ct);
            payloadLen = 0;
            for (var i = 0; i < 8; i++)
                payloadLen = (payloadLen << 8) | ext[i];
        }

        byte[]? mask = null;
        if (masked)
        {
            mask = new byte[4];
            await ReadExact(mask, ct);
        }

        var payload = new byte[payloadLen];

        if (payloadLen > 0)
            await ReadExact(payload, ct);

        if (mask is not null)
        {
            for (var i = 0; i < payload.Length; i++)
                payload[i] ^= mask[i % 4];
        }

        return new WebSocketFrame(opcode, fin, payload);
    }

    private async Task ReadExact(byte[] buffer, CancellationToken ct)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);

            if (read == 0)
                throw new EndOfStreamException();

            offset += read;
        }
    }
}
