using System.Net;
using System.Text.Json;
using Arbiter.Api.Formatters;
using Arbiter.Api.Http;

namespace Arbiter.Api.Results;

public class JsonResult(object? value, HttpStatusCode statusCode = HttpStatusCode.OK) : IActionResult
{
    public object? Value
    {
        get;
    } = value;
    public HttpStatusCode StatusCode
    {
        get;
    } = statusCode;

    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = (int)StatusCode;

        if (context.OutputFormatterSelector is { } selector)
        {
            var formatterContext = new OutputFormatterContext {
                HttpContext = context,
                ObjectType = Value?.GetType() ?? typeof(object),
                Object = Value,
            };

            var formatter = selector.Select(formatterContext);
            if (formatter is not null && formatter.CanWrite(formatterContext.ObjectType, "application/json"))
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