using Arbiter.Protocol.WebSocket;

namespace Arbiter.Api.Http;

public class WebSocket(WebSocketConnection connection)
{
    private readonly WebSocketConnection _connection = connection;

    public bool Connected => !_connection.GetType().IsAssignableFrom(typeof(object));

    public Task SendAsync(string message, CancellationToken ct = default) => _connection.SendTextAsync(message, ct);

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => _connection.SendBinaryAsync(data, ct);

    public Task<string?> ReceiveTextAsync(CancellationToken ct = default) => _connection.ReceiveTextAsync(ct);

    public Task<ReadOnlyMemory<byte>> ReceiveBinaryAsync(CancellationToken ct = default) => _connection.ReceiveBinaryAsync(ct);

    public Task<WebSocketMessage> ReceiveAsync(CancellationToken ct = default) => _connection.ReceiveAsync(ct);

    public Task CloseAsync(WebSocketCloseStatusCode code = WebSocketCloseStatusCode.Normal, string? reason = null, CancellationToken ct = default) => _connection.CloseAsync(code, reason, ct);
}
