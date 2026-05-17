using System.Buffers;

namespace Arlirad.WebSocket;

public class WebSocketFrameWriter(Stream stream)
{
    public async Task WriteFrame(WebSocketOpcode opcode, bool fin, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var maskBit = 0x00;
        var payloadLen = payload.Length;
        var headerSize = 2;
        int extLen;

        switch (payloadLen)
        {
            case <= 125:
                extLen = 0;
                break;
            case <= 65535:
                extLen = 2;
                headerSize += 2;
                break;
            default:
                extLen = 8;
                headerSize += 8;
                break;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(headerSize);

        try
        {
            buffer[0] = (byte)((fin ? 0x80 : 0x00) | ((int)opcode & 0x0F));

            switch (extLen)
            {
                case 0:
                    buffer[1] = (byte)(maskBit | payloadLen);
                    break;
                case 2:
                    buffer[1] = (byte)(maskBit | 126);
                    buffer[2] = (byte)((payloadLen >> 8) & 0xFF);
                    buffer[3] = (byte)(payloadLen & 0xFF);
                    break;
                default:
                    {
                        buffer[1] = (byte)(maskBit | 127);
                        var len = (ulong)payloadLen;
                        for (var i = 0; i < 8; i++)
                            buffer[2 + i] = (byte)((len >> (56 - (i * 8))) & 0xFF);
                        break;
                    }
            }

            await stream.WriteAsync(buffer.AsMemory(0, headerSize), ct);

            if (payloadLen > 0)
                await stream.WriteAsync(payload, ct);

            await stream.FlushAsync(ct);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task WriteClose(WebSocketCloseStatusCode code = WebSocketCloseStatusCode.Normal, string? reason = null, CancellationToken ct = default)
    {
        var reasonBytes = reason is not null ? System.Text.Encoding.UTF8.GetBytes(reason) : [];
        var payload = ArrayPool<byte>.Shared.Rent(2 + reasonBytes.Length);

        try
        {
            payload[0] = (byte)(((ushort)code >> 8) & 0xFF);
            payload[1] = (byte)((ushort)code & 0xFF);
            reasonBytes.CopyTo(payload, 2);
            await WriteFrame(WebSocketOpcode.Close, true, payload.AsMemory(0, 2 + reasonBytes.Length), ct);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(payload);
        }
    }

    public async Task WriteText(string text, CancellationToken ct = default) => await WriteFrame(WebSocketOpcode.Text, true, System.Text.Encoding.UTF8.GetBytes(text), ct);
    public async Task WriteBinary(ReadOnlyMemory<byte> data, CancellationToken ct = default) => await WriteFrame(WebSocketOpcode.Binary, true, data, ct);
    public async Task WritePing(ReadOnlyMemory<byte> data, CancellationToken ct = default) => await WriteFrame(WebSocketOpcode.Ping, true, data, ct);
    public async Task WritePong(ReadOnlyMemory<byte> data, CancellationToken ct = default) => await WriteFrame(WebSocketOpcode.Pong, true, data, ct);
}
