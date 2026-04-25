namespace Arbiter.Api.Http;

public class WebSocket(Stream stream, bool isServer)
{
    private readonly bool _isServer = isServer;
    private readonly Stream _stream = stream;

    public bool Connected => true;

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);
        await _stream.WriteAsync(bytes, ct);
    }

    public async Task<int> ReceiveAsync(byte[] buffer, CancellationToken ct = default) => await _stream.ReadAsync(buffer, ct);

    public Task CloseAsync() => Task.CompletedTask;
}