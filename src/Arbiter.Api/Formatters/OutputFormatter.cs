using System.Text;
using System.Text.Json;
using Arbiter.Api.Http;

namespace Arbiter.Api.Formatters;

public interface IOutputFormatter
{
    bool CanWrite(Type objectType, string? contentType);
    Task WriteAsync(object? value, HttpContext context);
}

public class SystemTextJsonOutputFormatter(JsonSerializerOptions options) : IOutputFormatter
{
    private readonly JsonSerializerOptions _options = options;

    public bool CanWrite(Type objectType, string? contentType)
    {
        return contentType is null ||
            contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
    }

    public async Task WriteAsync(object? value, HttpContext context)
    {
        context.Response.ContentType = "application/json";

        if (value is null)
            return;

        var json = JsonSerializer.Serialize(value, _options);
        await context.Response.WriteAsync(json);
    }
}

public class TextPlainOutputFormatter : IOutputFormatter
{
    public bool CanWrite(Type objectType, string? contentType)
    {
        return contentType?.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase) == true ||
            objectType == typeof(string) ||
            objectType.IsPrimitive;
    }

    public async Task WriteAsync(object? value, HttpContext context)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        var text = value?.ToString() ?? "";
        await context.Response.WriteAsync(text, Encoding.UTF8);
    }
}

public class OutputFormatterContext
{
    public required HttpContext HttpContext
    {
        get;
        init;
    }
    public required Type ObjectType
    {
        get;
        init;
    }
    public object? Object
    {
        get;
        init;
    }
}

public class OutputFormatterSelector
{
    private readonly List<IOutputFormatter> _formatters = [];

    public void Add(IOutputFormatter formatter) => _formatters.Add(formatter);

    public IOutputFormatter? Select(OutputFormatterContext context)
    {
        var acceptHeader = context.HttpContext.Request.Headers.Accept;
        var contentType = acceptHeader?.Split(',').FirstOrDefault()?.Trim()
            ?? "application/json";

        foreach (var formatter in _formatters)
        {
            if (formatter.CanWrite(context.ObjectType, contentType))
                return formatter;
        }

        return _formatters.FirstOrDefault(f => f.CanWrite(context.ObjectType, null));
    }
}