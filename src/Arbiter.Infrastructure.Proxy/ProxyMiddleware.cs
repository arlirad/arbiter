using System.Net;
using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Enums;
using Arbiter.Domain.Interfaces;
using Arbiter.Infrastructure.Proxy.Mappers;
using Arbiter.Infrastructure.Proxy.Models;
using Arbiter.Infrastructure.Streams;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Infrastructure.Proxy;

public class ProxyMiddleware : IMiddleware
{
    private readonly List<string> _disallowedHeaders =
    [
        "accept-encoding",
        "content-encoding",
        "connection",
        "keep-alive",
        "proxy-authenticate",
        "proxy-authorization",
        "trailer",
        "transfer-encoding",
        "upgrade",
    ];

    private HttpClient _client = null!;
    private Uri _target = null!;

    public Task Configure(Site site, IConfiguration config)
    {
        var typedConfig = config.Get<ConfigModel>();

        if (typedConfig?.Target is null)
            throw new Exception("target is not set");

        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
        };

        _client = new HttpClient(handler, disposeHandler: false);
        _target = typedConfig.Target;

        return Task.CompletedTask;
    }

    public async Task Handle(Context context)
    {
        if (context.Request.Method == Method.Connect)
        {
            await context.Response.Set(Status.MethodNotAllowed);
            return;
        }

        var targetPath = _target.AbsolutePath.TrimEnd('/') + '/' + context.Request.Path.TrimStart('/');
        var targetUri = new Uri(_target, targetPath);
        var method = MethodMapper.ToHttpMethod(context.Request.Method);
        var targetRequest = new HttpRequestMessage(method, targetUri)
        {
            Content = context.Request.Stream is not null ? new StreamContent(context.Request.Stream) : null,
        };

        List<string>? connectionHeaders = null;

        foreach (var header in context.Request.Headers)
        {
            if (ShouldIgnoreHeader(header.Key, header.Value, ref connectionHeaders))
                continue;

            if (!targetRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
                targetRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        try
        {
            await SendRequest(context, targetRequest);
        }
        catch (HttpRequestException)
        {
            await context.Response.Set(Status.BadGateway);
        }
    }

    private async Task SendRequest(Context context, HttpRequestMessage targetRequest)
    {
        var response = await _client.SendAsync(targetRequest, HttpCompletionOption.ResponseHeadersRead);
        var responseStream = await response.Content.ReadAsStreamAsync();

        var status = StatusCodeMapper.FromHttpStatusCode(response.StatusCode);

        if (!status.HasValue)
        {
            await context.Response.Set(Status.BadGateway);
            return;
        }

        CopyHeaders(context, response);

        if (response.Content.Headers.ContentLength.HasValue)
            responseStream = new ClampedStream(responseStream, response.Content.Headers.ContentLength.Value);

        await context.Response.Set(status.Value, responseStream);
    }

    private void CopyHeaders(Context context, HttpResponseMessage response)
    {
        List<string>? connectionHeaders = null;

        foreach (var header in response.Headers)
        {
            var valueList = header.Value.ToList();

            if (ShouldIgnoreHeader(header.Key, valueList, ref connectionHeaders))
                continue;

            context.Response.Headers[header.Key] = valueList;
        }

        if (response.Content.Headers.ContentType == null)
            return;

        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(response.Content.Headers.ContentType.MediaType))
            segments.Add(response.Content.Headers.ContentType.MediaType);

        if (!string.IsNullOrWhiteSpace(response.Content.Headers.ContentType.CharSet))
            segments.Add($"charset={response.Content.Headers.ContentType.CharSet}");

        if (segments.Count > 0)
            context.Response.Headers.ContentType = string.Join("; ", segments);
    }

    private bool ShouldIgnoreHeader(string key, List<string> values, ref List<string>? connectionHeaders)
    {
        if (key.Equals("te", StringComparison.OrdinalIgnoreCase))
            if (values.Any(v => v == "trailers"))
                return true;

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

        if (connectionHeaders != null && connectionHeaders.Contains(key, StringComparer.OrdinalIgnoreCase))
            return true;

        return _disallowedHeaders.Contains(key, StringComparer.OrdinalIgnoreCase);
    }
}