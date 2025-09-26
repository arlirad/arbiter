using System.Net;
using System.Net.Sockets;
using Arbiter.DTOs;
using Arbiter.Mappers;

namespace Arbiter.Infrastructure.Network;

internal class Session(Socket socket)
{
    private Stream _stream = new NetworkStream(socket);
    private bool _inSsl = false;

    public async Task<SessionReceiveResult> Receive()
    {
        if (!_inSsl && await CheckForSsl(socket))
            _stream = await WrapInSsl();

        using var reader = new StreamReader(_stream, leaveOpen: true);

        try
        {
            var header = await reader.ReadLineAsync();
            if (header is null)
                return SessionReceiveResult.Closed();

            var headerSplit = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (headerSplit.Length < 3)
                return SessionReceiveResult.BadRequest();

            var method = HttpMethodMapper.ToEnum(headerSplit[0]);
            var uri = headerSplit[1];
            var version = HttpVersionMapper.ToEnum(headerSplit[2]);

            if (!method.HasValue || !version.HasValue || uri.Contains(".."))
                return SessionReceiveResult.BadRequest();

            var headers = await GetHeaders(reader);

            if (headers is null)
                return SessionReceiveResult.BadRequest();

            var port = (socket.LocalEndPoint as IPEndPoint)?.Port ?? 0;
            var request = new HttpRequest(method.Value, uri, version.Value, headers);

            return SessionReceiveResult.Ok(request, _stream, _inSsl, port);
        }
        catch (Exception e)
        {
            return SessionReceiveResult.Closed();
        }
    }

    private static async Task<HttpHeaders?> GetHeaders(StreamReader reader)
    {
        var headers = new HttpHeaders();

        while (true)
        {
            var line = await reader.ReadLineAsync();

            if (line is null)
                return null;

            if (line.Length == 0)
                break;

            var keyValueSeperatorIndex = line.IndexOf(": ");

            headers[line[0..keyValueSeperatorIndex]] = line[(keyValueSeperatorIndex + 2)..];
        }

        return headers;
    }

    private static async Task<bool> CheckForSsl(Socket socket)
    {
        var buffer = new byte[1];
        var length = await socket.ReceiveAsync(buffer, SocketFlags.Peek);

        if (length == 0)
            return false;

        return buffer[0] == 22;
    }

    private async Task<Stream> WrapInSsl()
    {
        throw new NotImplementedException();
    }
}