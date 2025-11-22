using Arbiter.Infrastructure.Enums;
using Arbiter.Infrastructure.Mappers;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Domain.Enums;
using Arbiter.Domain.ValueObjects;
using Arbiter.Infrastructure.Streams;
using Arbiter.Transport.Tcp.Streams;

namespace Arbiter.Transport.Tcp;

internal class TcpTransaction(Stream stream, bool isSsl, int port) : ITransaction
{
    private const string NewLine = "\r\n";

    private readonly TaskCompletionSource _tcs = new();
    private Method _requestMethod;
    private Stream? _responseStream;
    private HttpVersion _version = HttpVersion.Http11;

    internal bool Finished { get; set; }
    internal bool Faulted { get; set; }
    internal Task ResponseSet { get => _tcs.Task; }

    public bool IsSecure { get => isSsl; }
    public int Port { get => port; }

    public async Task<RequestDto?> GetRequest()
    {
        var (headerStream, remainder) = await HeadersFinder.GetHeadersClampedStream(stream);
        if (headerStream is null)
            return null;

        var reader = new StreamReader(headerStream);

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

        Stream? requestBodyStream = null;
        var contentLengthString = headers["content-length"];

        if (!string.IsNullOrWhiteSpace(contentLengthString))
        {
            if (!int.TryParse(contentLengthString, out var length))
                return null;

            var remainderStream = new RemainderStream(stream, remainder);
            requestBodyStream = new ClampedStream(remainderStream, length);
        }

        headers["content-length"] = null;

        _requestMethod = method.Value;

        return new RequestDto
        {
            Method = method.Value,
            Authority = host,
            Path = path,
            Headers = new ReadOnlyHeaders(headers),
            Stream = requestBodyStream,
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

                if (_responseStream.CanSeek || _responseStream is ClampedStream)
                    await writer.WriteLineAsync($"Content-Length: {_responseStream.Length}");
            }
            else if (ShouldSendZeroContentLength(response.Status))
            {
                await writer.WriteLineAsync("Content-Length: 0");
            }

            await writer.WriteLineAsync();
        }

        _ = Finish();
    }

    private bool ShouldSendZeroContentLength(Status status)
    {
        return (int)status switch
        {
            >= 100 and <= 199 => false,
            204 => false,
            >= 200 and <= 299 => _requestMethod != Method.Connect,
            _ => true,
        };
    }

    private async Task Finish()
    {
        if (_responseStream is not null)
        {
            await _responseStream.CopyToAsync(stream);
            await _responseStream.FlushAsync();
        }

        _tcs.SetResult();
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