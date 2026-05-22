using System.Text.Json;
using Arbiter.Api.Formatters;
using Arbiter.Api.Http;

namespace Arbiter.Api.Results;

public class NotFoundObjectResult(object? value) : IActionResult
{
    public object? Value
    {
        get;
    } = value;

    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = 404;

        if (context.OutputFormatterSelector is { } selector)
        {
            var formatterContext = new OutputFormatterContext {
                HttpContext = context,
                ObjectType = Value?.GetType() ?? typeof(object),
                Object = Value,
            };

            var formatter = selector.Select(formatterContext);

            if (formatter is not null)
            {
                await formatter.WriteAsync(Value, context);

                return;
            }
        }

        context.Response.ContentType = "application/json";

        if (Value is not null)
        {
            var json = JsonSerializer.Serialize(Value, context.JsonSerializerOptions);
            await context.Response.WriteAsync(json);
        }
    }
}
