using Arbiter.Infrastructure.Enums;
using Arbiter.Infrastructure.Mappers;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Streams;
using Arlirad.Ervi.Net.Http;

namespace Arbiter.Transport.Tcp;

internal class TcpTransaction(Stream stream, bool isSsl, int port, CancellationToken ct) : ITransaction
{
    private const string NewLine = "\r\n";
    private const string ChunkedEncoding = "chunked";

    private readonly TaskCompletionSource _tcs = new();
    private readonly TaskCompletionSource _upgradeTcs = new();
    private bool _chunked;
    private Method _requestMethod;
    private Stream? _responseStream;
    private HttpVersion _version = HttpVersion.Http11;

    internal bool Finished { get; set; }
    internal bool Faulted { get; set; }
    internal bool Upgraded { get; set; }
    internal Task ResponseSet { get => _tcs.Task; }
    internal Task UpgradeCompleted { get => _upgradeTcs.Task; }

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

        var headers = await HeadersFinder.ParseHeaders(reader);
        if (headers is null)
            return null;

        var host = headers.Host;

        if (version == HttpVersion.Http11 && host is null)
            return null;

        _version = version.Value;
        headers.Host = null;

        var requestBodyStream = GetBodyStream(headers, remainder);

        _requestMethod = method.Value;

        var isWebSocketUpgrade = DetectWebSocketUpgrade(method.Value, headers);

        return new RequestDto
        {
            Method = method.Value,
            Authority = host,
            Path = path,
            Headers = new ReadOnlyHeaders(headers),
            Stream = requestBodyStream,
            IsWebSocketUpgrade = isWebSocketUpgrade,
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

                foreach (var instance in header.Value)
                {
                    await writer.WriteLineAsync($"{header.Key}: {instance}");
                }
            }

            if (response.Status != Status.SwitchingProtocol && response.Stream is not null)
            {
                _responseStream = response.Stream;

                if (_responseStream.CanSeek || _responseStream is ClampedStream)
                {
                    await writer.WriteLineAsync($"Content-Length: {_responseStream.Length}");
                }
                else
                {
                    await writer.WriteLineAsync($"Transfer-Encoding: {ChunkedEncoding}");
                    _chunked = true;
                }
            }
            else if (ShouldSendZeroContentLength(response.Status))
            {
                await writer.WriteLineAsync("Content-Length: 0");
            }

            await writer.WriteLineAsync();
        }

        if (response.Status == Status.SwitchingProtocol && response.Stream is not null)
        {
            Upgraded = true;
            _ = RunUpgrade(response.Stream);
            return;
        }

        _ = Finish();
    }

    private Stream? GetBodyStream(Headers headers, Stream? remainder)
    {
        var contentLengthString = headers.ContentLength;
        var transferEncoding = headers.TransferEncoding;

        if (!string.IsNullOrWhiteSpace(transferEncoding))
        {
            if (transferEncoding.Equals(ChunkedEncoding, StringComparison.OrdinalIgnoreCase))
            {
                var remainderStream = new RemainderStream(stream, remainder);

                return new HttpChunkedStream(remainderStream);
            }
        }
        else if (!string.IsNullOrWhiteSpace(contentLengthString))
        {
            if (!int.TryParse(contentLengthString, out var length))
                return null;

            var remainderStream = new RemainderStream(stream, remainder);

            return new ClampedStream(remainderStream, length);
        }

        return null;
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
            if (_chunked)
            {
                await using var wrapped = new HttpChunkedStream(stream);
                await _responseStream.CopyToAsync(wrapped);
            }
            else
            {
                await _responseStream.CopyToAsync(stream);
            }

            await stream.FlushAsync();
        }

        _tcs.SetResult();
    }

    private async Task RunUpgrade(Stream responseStream)
    {
        _tcs.SetResult();

        try
        {
            await StreamRelay.BidirectionalCopy(responseStream, stream, ct);
        }
        catch
        {
        }
        finally
        {
            await responseStream.DisposeAsync();
            _upgradeTcs.SetResult();
        }
    }

    private static bool DetectWebSocketUpgrade(Method method, Headers headers)
    {
        if (method != Method.Get)
            return false;

        var connection = headers["connection"]?
                .SelectMany(v => v.Split(','))
                .Select(v => v.Trim())
            ?? [];

        if (!connection.Any(v => v.Equals("upgrade", StringComparison.OrdinalIgnoreCase)))
            return false;

        var upgrade = headers["upgrade"]?.FirstOrDefault() ?? "";

        return upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase);
    }
}