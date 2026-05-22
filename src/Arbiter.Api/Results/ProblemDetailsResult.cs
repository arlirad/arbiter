using System.Text.Json;
using Arbiter.Api.Formatters;
using Arbiter.Api.Http;

namespace Arbiter.Api.Results;

public class ProblemDetailsResult(ProblemDetails problemDetails) : IActionResult
{
    public ProblemDetails ProblemDetails
    {
        get;
    } = problemDetails;

    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = ProblemDetails.Status ?? 500;

        if (context.OutputFormatterSelector is { } selector)
        {
            var formatterContext = new OutputFormatterContext {
                HttpContext = context,
                ObjectType = typeof(ProblemDetails),
                Object = ProblemDetails,
            };

            var formatter = selector.Select(formatterContext);

            if (formatter is not null && formatter.CanWrite(typeof(ProblemDetails), "application/problem+json"))
            {
                await formatter.WriteAsync(ProblemDetails, context);

                return;
            }
        }

        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(ProblemDetails, context.JsonSerializerOptions);
        await context.Response.WriteAsync(json);
    }
}
