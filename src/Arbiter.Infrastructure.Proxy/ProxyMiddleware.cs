using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Proxy.Connectors;
using Arbiter.Infrastructure.Proxy.Mappers;
using Arbiter.Infrastructure.Proxy.Models;
using Arbiter.Infrastructure.Streams;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Infrastructure.Proxy;

public class ProxyMiddleware : IMiddleware
{
    private static readonly HashSet<string> DisallowedHeaders =
        new(
            [
                "accept-encoding",
                "content-encoding",
                "connection",
                "expect",
                "keep-alive",
                "proxy-authenticate",
                "proxy-authorization",
                "trailer",
                "transfer-encoding",
            ],
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> DisallowedUpgradeHeaders =
        new(DisallowedHeaders.Where(h => !h.Equals("connection", StringComparison.OrdinalIgnoreCase))
                .Append("host"),
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> DisallowedResponseContentHeaders =
        new(["content-length", "content-type"], StringComparer.OrdinalIgnoreCase);

    private HttpClient _client = null!;
    private IProxyConnector _connector = null!;

    public Task Configure(string path, ComponentDataContainer data, IConfiguration config)
    {
        var typedConfig = config.Get<ConfigModel>();

        if (typedConfig?.Target is null)
            throw new Exception("target is not set");

        _connector = typedConfig.Target.Scheme.Equals("unix", StringComparison.OrdinalIgnoreCase)
            ? new UnixProxyConnector(typedConfig.Target.AbsolutePath)
            : new TcpProxyConnector(typedConfig.Target);

        _client = new HttpClient(_connector.CreateHandler(), false);

        return Task.CompletedTask;
    }

    public async Task Handle(Context context)
    {
        if (context.Request.Upgrade is IWebSocketUpgrade upgrade)
        {
            await HandleWebSocket(context, upgrade);

            return;
        }

        if (context.Request.Upgrade is not null)
        {
            await context.Response.Set(Status.NotImplemented);

            return;
        }

        if (context.Request.Method == Method.Connect)
        {
            await context.Response.Set(Status.MethodNotAllowed);

            return;
        }

        var targetUri = _connector.BuildTargetUri(context.Request.Path);
        var method = MethodMapper.ToHttpMethod(context.Request.Method);
        var targetRequest = new HttpRequestMessage(method, targetUri) {
            Content = context.Request.Stream is not null ? new StreamContent(context.Request.Stream) : null,
        };

        List<string>? connectionHeaders = null;

        foreach (var header in context.Request.Headers)
        {
            if (ShouldIgnoreHeader(header.Key, header.Value, ref connectionHeaders, DisallowedHeaders))
                continue;

            if (!targetRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
                targetRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        AddForwardingHeaders(targetRequest.Headers, context);

        try
        {
            await SendRequest(context, targetRequest);
        }
        catch (Exception)
        {
            await context.Response.Set(Status.BadGateway);
        }
    }

    private async Task HandleWebSocket(Context context, IWebSocketUpgrade upgrade)
    {
        var targetUri = _connector.BuildTargetUri(context.Request.Path);
        var host = _connector.GetHost(context);
        var connection = await _connector.ConnectForUpgradeAsync();

        try
        {
            var sb = new StringBuilder(256);
            sb.Append("GET ");
            sb.Append(targetUri.PathAndQuery);
            sb.Append(" HTTP/1.1\r\nHost: ");
            sb.Append(host);
            sb.Append("\r\n");

            AppendHeaderIfMissing(sb, context.Request.Headers, "Upgrade", "websocket");
            AppendHeaderIfMissing(sb, context.Request.Headers, "Connection", "Upgrade");
            AppendHeaderIfMissing(sb, context.Request.Headers, "Sec-WebSocket-Key", CreateWebSocketKey());
            AppendHeaderIfMissing(sb, context.Request.Headers, "Sec-WebSocket-Version", "13");

            foreach (var header in context.Request.Headers)
            {
                if (DisallowedUpgradeHeaders.Contains(header.Key))
                    continue;

                foreach (var value in header.Value)
                {
                    sb.Append(header.Key);
                    sb.Append(": ");
                    sb.Append(value);
                    sb.Append("\r\n");
                }
            }

            AppendForwardingHeaders(sb, context);

            sb.Append("\r\n");

            var requestBytes = Encoding.UTF8.GetBytes(sb.ToString());
            await connection.Stream.WriteAsync(requestBytes);
            await connection.Stream.FlushAsync();

            var (status, responseHeaders, responseStream) = await ReadUpgradeResponse(connection.Stream);

            if (!status.HasValue)
            {
                await context.Response.Set(Status.BadGateway);

                return;
            }

            if (status != Status.SwitchingProtocol)
            {
                CopyHeaders(context, responseHeaders);
                await context.Response.Set(status.Value, responseStream);

                return;
            }

            if (responseStream is null)
            {
                await context.Response.Set(Status.BadGateway);

                return;
            }

            var clientStream = await upgrade.AcceptAsync(new ReadOnlyHeaders(responseHeaders));
            context.IsUpgraded = true;
            connection.Detach();

            try
            {
                await StreamRelay.BidirectionalCopy(responseStream, clientStream);
            }
            finally
            {
                await responseStream.DisposeAsync();
                await clientStream.DisposeAsync();
            }
        }
        finally
        {
            connection.Dispose();
        }
    }

    private static string CreateWebSocketKey()
    {
        Span<byte> nonce = stackalloc byte[16];
        RandomNumberGenerator.Fill(nonce);

        return Convert.ToBase64String(nonce);
    }

    private static void AppendHeaderIfMissing(StringBuilder sb, ReadOnlyHeaders headers, string name, string value)
    {
        if (headers[name] is not null)
            return;

        sb.Append(name);
        sb.Append(": ");
        sb.Append(value);
        sb.Append("\r\n");
    }

    private static async Task<(Status?, Headers, Stream?)> ReadUpgradeResponse(Stream stream)
    {
        var (headers, remainder) = await HeadersFinder.GetHeadersClampedStream(stream);

        if (headers is null)
            return (null, new Headers(), null);

        using var reader = new StreamReader(headers);
        var statusLine = await reader.ReadLineAsync();

        if (statusLine is null)
            return (null, new Headers(), null);

        var responseHeaders = await HeadersFinder.ParseHeaders(reader);

        if (responseHeaders is null)
            return (null, new Headers(), null);

        var status = ParseStatus(statusLine);
        var responseStream = status == Status.SwitchingProtocol
            ? remainder is not null ? new RemainderStream(stream, remainder) : stream
            : CreateResponseBodyStream(responseHeaders, stream, remainder);

        return (status, responseHeaders, responseStream);
    }

    private static void CopyHeaders(Context context, Headers responseHeaders)
    {
        List<string>? connectionHeaders = null;

        foreach (var header in responseHeaders)
        {
            if (ShouldIgnoreHeader(header.Key, header.Value, ref connectionHeaders, DisallowedHeaders))
                continue;

            context.Response.Headers[header.Key] = header.Value;
        }
    }

    private static Status? ParseStatus(string statusLine)
    {
        var parts = statusLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 2 && int.TryParse(parts[1], out var statusCode) && Enum.IsDefined(typeof(Status), statusCode)
            ? (Status)statusCode
            : null;
    }

    private static Stream? CreateResponseBodyStream(Headers headers, Stream stream, Stream? remainder)
    {
        var contentLength = headers.ContentLength;

        return !string.IsNullOrWhiteSpace(contentLength)
            ? long.TryParse(contentLength, out var length) && length > 0
                ? new ClampedStream(new RemainderStream(stream, remainder), length)
                : null
            : remainder is not null ? new RemainderStream(stream, remainder) : null;
    }

    private async Task SendRequest(Context context, HttpRequestMessage targetRequest)
    {
        var response = await _client.SendAsync(targetRequest, HttpCompletionOption.ResponseHeadersRead);

        var status = StatusCodeMapper.FromHttpStatusCode(response.StatusCode);

        if (!status.HasValue)
        {
            response.Dispose();
            await context.Response.Set(Status.BadGateway);

            return;
        }

        CopyHeaders(context, response);

        var responseStream = await response.Content.ReadAsStreamAsync();

        if (response.Content.Headers.ContentLength.HasValue)
            responseStream = new ClampedStream(responseStream, response.Content.Headers.ContentLength.Value);

        await context.Response.Set(status.Value, new ResponseStream(responseStream, response));
    }

    private static void CopyHeaders(Context context, HttpResponseMessage response)
    {
        List<string>? connectionHeaders = null;

        foreach (var header in response.Headers)
        {
            var valueList = header.Value.ToList();

            if (ShouldIgnoreHeader(header.Key, valueList, ref connectionHeaders, DisallowedHeaders))
                continue;

            context.Response.Headers[header.Key] = valueList;
        }

        foreach (var header in response.Content.Headers)
        {
            if (DisallowedResponseContentHeaders.Contains(header.Key))
                continue;

            context.Response.Headers[header.Key] = [.. header.Value];
        }

        if (response.Content.Headers.ContentType is null)
            return;

        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(response.Content.Headers.ContentType.MediaType))
            segments.Add(response.Content.Headers.ContentType.MediaType);

        if (!string.IsNullOrWhiteSpace(response.Content.Headers.ContentType.CharSet))
            segments.Add($"charset={response.Content.Headers.ContentType.CharSet}");

        if (segments.Count > 0)
            context.Response.Headers.ContentType = string.Join("; ", segments);
    }

    private static bool ShouldIgnoreHeader(
        string key,
        List<string> values,
        ref List<string>? connectionHeaders,
        IEnumerable<string> disallowedHeaders)
    {
        if (key.Equals("te", StringComparison.OrdinalIgnoreCase))
        {
            if (values.Any(v => v == "trailers"))
                return true;
        }

        if (key.Equals("connection", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var value in values)
            {
                var headers = value.Split(',')
                    .Where(h => h.Equals("keep-alive", StringComparison.OrdinalIgnoreCase)
                        && !h.Equals("close", StringComparison.OrdinalIgnoreCase))
                    .Select(h => h.Trim())
                    .ToList();

                (connectionHeaders ??= []).AddRange(headers);
            }

            return true;
        }

        return (connectionHeaders != null && connectionHeaders.Contains(key, StringComparer.OrdinalIgnoreCase)) || disallowedHeaders.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    private static void AddForwardingHeaders(HttpRequestHeaders headers, Context context)
    {
        var proto = context.Request.IsSecure ? "https" : "http";

        if (context.Request.RemoteAddress is not null)
        {
            var ip = context.Request.RemoteAddress.ToString();
            headers.TryAddWithoutValidation("X-Real-IP", [ip]);
            headers.TryAddWithoutValidation("X-Forwarded-For", [ip]);
        }

        headers.TryAddWithoutValidation("X-Forwarded-Proto", [proto]);
        headers.TryAddWithoutValidation("X-Forwarded-Protocol", [proto]);

        if (context.Request.Authority is not null)
            headers.TryAddWithoutValidation("X-Forwarded-Host", [context.Request.Authority]);
    }

    private static void AppendForwardingHeaders(StringBuilder sb, Context context)
    {
        var proto = context.Request.IsSecure ? "https" : "http";

        if (context.Request.RemoteAddress is not null)
        {
            var ip = context.Request.RemoteAddress.ToString();
            sb.Append("X-Real-IP: ");
            sb.Append(ip);
            sb.Append("\r\n");

            sb.Append("X-Forwarded-For: ");
            sb.Append(ip);
            sb.Append("\r\n");
        }

        sb.Append("X-Forwarded-Proto: ");
        sb.Append(proto);
        sb.Append("\r\n");

        sb.Append("X-Forwarded-Protocol: ");
        sb.Append(proto);
        sb.Append("\r\n");

        if (context.Request.Authority is not null)
        {
            sb.Append("X-Forwarded-Host: ");
            sb.Append(context.Request.Authority);
            sb.Append("\r\n");
        }
    }
}
