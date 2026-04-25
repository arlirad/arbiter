using System.Net;
using Arbiter.Api.Http;

namespace Arbiter.Api.Results;

public class StatusCodeResult(HttpStatusCode statusCode) : IActionResult
{
    public HttpStatusCode StatusCode
    {
        get;
    } = statusCode;

    public Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = (int)StatusCode;
        return Task.CompletedTask;
    }
}