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
        var targetRequest = new HttpRequestMessage(method, targetUri);

        foreach (var header in context.Request.Headers)
        {
            var values = new string[1] { header.Value };

            if (!targetRequest.Headers.TryAddWithoutValidation(header.Key, values))
                targetRequest.Content?.Headers.TryAddWithoutValidation(header.Key, values);
        }

        try
        {
            await SendRequest(context, targetRequest);
        }
        catch (HttpRequestException)
        {
            await context.Response.Set(Status.BadGateway);
            return;
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

        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.First();
        }

        if (response.Content.Headers.ContentType != null)
        {
            var segments = new List<string>();

            if (!string.IsNullOrWhiteSpace(response.Content.Headers.ContentType.MediaType))
                segments.Add(response.Content.Headers.ContentType.MediaType);

            if (!string.IsNullOrWhiteSpace(response.Content.Headers.ContentType.CharSet))
                segments.Add($"charset={response.Content.Headers.ContentType.CharSet}");

            if (segments.Count > 0)
                context.Response.Headers["content-type"] = string.Join("; ", segments);
        }

        if (response.Content.Headers.ContentLength.HasValue)
            responseStream = new ClampedStream(responseStream, response.Content.Headers.ContentLength.Value);

        await context.Response.Set(status.Value, responseStream);
    }
}