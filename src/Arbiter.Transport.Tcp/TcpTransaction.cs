using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Domain.ValueObjects;
using Arbiter.Transport.Tcp.Enums;
using Arbiter.Transport.Tcp.Mappers;

namespace Arbiter.Transport.Tcp;

internal class TcpTransaction(Stream stream, bool isSsl, int port) : ITransaction
{
    private const string NewLine = "\r\n";

    private readonly TaskCompletionSource _tcs = new();
    private Stream? _responseStream;
    private HttpVersion _version = HttpVersion.Http11;

    internal bool Finished { get; set; }
    internal bool Faulted { get; set; }
    internal Task ResponseSet { get => _tcs.Task; }

    public bool IsSecure { get => isSsl; }
    public int Port { get => port; }

    public async Task<RequestDto?> GetRequest()
    {
        var reader = new StreamReader(stream, leaveOpen: true);

        var requestLine = await reader.ReadLineAsync();
        if (requestLine is null)
            return null;

        var headerSplit = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (headerSplit.Length < 3)
            return null;

        var method = MethodMapper.ToEnum(headerSplit[0]);
        var path = headerSplit[1];
        var version = VersionMapper.ToEnum(headerSplit[2]);

        if (!method.HasValue || !version.HasValue)
            return null;

        var headers = await GetHeaders(reader);
        if (headers is null)
            return null;

        var host = headers["host"];

        if (version == HttpVersion.Http11 && host is null)
            return null;

        _version = version.Value;
        headers["host"] = null;

        return new RequestDto
        {
            Method = method.Value,
            Authority = host,
            Path = path,
            Headers = new ReadOnlyHeaders(headers),
            Stream = null,
        };
    }

    public async Task SetResponse(ResponseDto response)
    {
        await using (var writer = new StreamWriter(stream, leaveOpen: true))
        {
            writer.NewLine = NewLine;

            var version = VersionMapper.ToString(_version);
            var statusCode = (int)response.Status;
            var statusPhrase = StatusCodeMapper.ToReasonPhrase(response.Status);
            var responseLine = $"{version} {statusCode} {statusPhrase}";

            await writer.WriteLineAsync(responseLine);

            foreach (var header in response.Headers)
            {
                if (header.Key.Equals("content-length", StringComparison.OrdinalIgnoreCase)
                    && response.Stream is not null)
                    continue;

                await writer.WriteLineAsync($"{header.Key}: {header.Value}");
            }

            if (response.Stream is not null)
            {
                _responseStream = response.Stream;

                if (_responseStream.CanSeek)
                    await writer.WriteLineAsync($"Content-Length: {_responseStream.Length}");
            }

            await writer.WriteLineAsync();
        }

        _tcs.SetResult();
    }

    public async Task Finalize()
    {
        if (_responseStream is not null)
        {
            await _responseStream.CopyToAsync(stream);
            await stream.FlushAsync();
        }
    }

    private static async Task<Headers?> GetHeaders(StreamReader reader)
    {
        var headers = new Headers();

        while (true)
        {
            var line = await reader.ReadLineAsync();

            if (line is null)
                return null;

            if (line.Length == 0)
                break;

            var keyValueSeparatorIndex = line.IndexOf(": ", StringComparison.Ordinal);

            headers[line[0..keyValueSeparatorIndex]] = line[(keyValueSeparatorIndex + 2)..];
        }

        return headers;
    }
}