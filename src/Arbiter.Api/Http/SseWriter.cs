using System.Text.Json;

namespace Arbiter.Api.Http;

public sealed class SseWriter(HttpContext context, JsonSerializerOptions? jsonOptions = null)
{
    private readonly JsonSerializerOptions _jsonOptions = jsonOptions ?? new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public HttpContext Context
    {
        get;
    } = context;

    public Task WriteAsync(SseEvent evt)
    {
        if (evt.Event is not null)
            _ = Context.Response.WriteAsync($"event: {evt.Event}\n");

        if (evt.Data is not null)
        {
            var data = evt.Data is string str ? str : JsonSerializer.Serialize(evt.Data, _jsonOptions);

            foreach (var line in data.Split('\n'))
                _ = Context.Response.WriteAsync($"data: {line}\n");
        }

        if (evt.Id is not null)
            _ = Context.Response.WriteAsync($"id: {evt.Id}\n");

        if (evt.Retry is { } retry)
            _ = Context.Response.WriteAsync($"retry: {retry}\n");

        return Context.Response.WriteAsync("\n");
    }
}
