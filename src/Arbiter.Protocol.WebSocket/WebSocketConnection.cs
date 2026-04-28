namespace Arlirad.WebSocket;

public class WebSocketConnection(Stream stream) : IAsyncDisposable
{
    private readonly WebSocketFrameReader _reader = new(stream);
    private readonly WebSocketFrameWriter _writer = new(stream);
    private readonly CancellationTokenSource _cts = new();
    private bool _closed;

    public async Task<string?> ReceiveTextAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
        var sb = new System.Text.StringBuilder();
        var first = true;
        var opcode = WebSocketOpcode.Continuation;

        while (true)
        {
            var frame = await _reader.ReadFrame(linked.Token);

            if (frame.Opcode == WebSocketOpcode.Ping)
            {
                await _writer.WritePong(frame.Payload, linked.Token);
                continue;
            }

            if (frame.Opcode == WebSocketOpcode.Close)
            {
                await CloseAsync(ct: linked.Token);
                return null;
            }

            if (first)
            {
                if (frame.Opcode != WebSocketOpcode.Text)
                    throw new InvalidOperationException($"Expected Text frame, got {frame.Opcode}");

                opcode = frame.Opcode;
                first = false;
            }

            sb.Append(System.Text.Encoding.UTF8.GetString(frame.Payload.Span));

            if (frame.Fin)
                break;
        }

        return sb.ToString();
    }

    public async Task<ReadOnlyMemory<byte>> ReceiveBinaryAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
        using var ms = new MemoryStream();
        var first = true;

        while (true)
        {
            var frame = await _reader.ReadFrame(linked.Token);

            if (frame.Opcode == WebSocketOpcode.Ping)
            {
                await _writer.WritePong(frame.Payload, linked.Token);
                continue;
            }

            if (frame.Opcode == WebSocketOpcode.Close)
            {
                await CloseAsync(ct: linked.Token);
                return ReadOnlyMemory<byte>.Empty;
            }

            if (first)
            {
                if (frame.Opcode != WebSocketOpcode.Binary)
                    throw new InvalidOperationException($"Expected Binary frame, got {frame.Opcode}");

                first = false;
            }

            if (frame.Payload.Length > 0)
                await ms.WriteAsync(frame.Payload, linked.Token);

            if (frame.Fin)
                break;
        }

        return ms.ToArray();
    }

    public async Task<WebSocketMessage> ReceiveAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
        using var ms = new MemoryStream();
        var first = true;
        var opcode = WebSocketOpcode.Continuation;

        while (true)
        {
            var frame = await _reader.ReadFrame(linked.Token);

            if (frame.Opcode == WebSocketOpcode.Ping)
            {
                await _writer.WritePong(frame.Payload, linked.Token);
                continue;
            }

            if (frame.Opcode == WebSocketOpcode.Close)
            {
                await CloseAsync(ct: linked.Token);
                return new WebSocketMessage(WebSocketOpcode.Close, ReadOnlyMemory<byte>.Empty);
            }

            if (first)
            {
                opcode = frame.Opcode;
                first = false;
            }

            if (frame.Payload.Length > 0)
                await ms.WriteAsync(frame.Payload, linked.Token);

            if (frame.Fin)
                break;
        }

        return new WebSocketMessage(opcode, ms.ToArray());
    }

    public Task SendTextAsync(string text, CancellationToken ct = default) => _writer.WriteText(text, ct);

    public Task SendBinaryAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => _writer.WriteBinary(data, ct);

    public async Task CloseAsync(WebSocketCloseStatusCode code = WebSocketCloseStatusCode.Normal, string? reason = null, CancellationToken ct = default)
    {
        if (_closed)
            return;

        _closed = true;

        try
        {
            await _writer.WriteClose(code, reason, ct);
        }
        catch
        {
            // ignored
        }

        await _cts.CancelAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_closed)
            await CloseAsync();

        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}

public readonly struct WebSocketMessage(WebSocketOpcode opcode, ReadOnlyMemory<byte> payload)
{
    public WebSocketOpcode Opcode
    {
        get;
    } = opcode;
    public ReadOnlyMemory<byte> Payload
    {
        get;
    } = payload;

    public string? AsText()
    {
        return Opcode == WebSocketOpcode.Text
            ? System.Text.Encoding.UTF8.GetString(Payload.ToArray())
            : null;
    }
}
