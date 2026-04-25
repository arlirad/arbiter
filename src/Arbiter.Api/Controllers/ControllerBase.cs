using System.Net;
using Arbiter.Api.Http;
using Arbiter.Api.Results;

namespace Arbiter.Api.Controllers;

public abstract class ControllerBase : IApiController
{
    public HttpContext HttpContext
    {
        get;
        internal set;
    } = null!;
    public ModelStateDictionary ModelState
    {
        get;
        private set;
    } = new();

    internal void SetModelState(ModelStateDictionary modelState) => ModelState = modelState;

    protected static OkResult Ok() => new();
    protected static OkObjectResult Ok(object? value) => new(value);
    protected static NotFoundResult NotFound() => new();
    protected static NotFoundObjectResult NotFound(object? value) => new(value);
    protected static BadRequestResult BadRequest() => new();
    protected static BadRequestObjectResult BadRequest(object? value) => new(value);
    protected static UnauthorizedResult Unauthorized() => new();
    protected static UnauthorizedObjectResult Unauthorized(object? value) => new(value);
    protected static NoContentResult NoContent() => new();
    protected static CreatedResult Created(string uri, object? value) => new(uri, value);
    protected static StatusCodeResult StatusCode(HttpStatusCode statusCode) => new(statusCode);
    protected static JsonResult Json(object? value, HttpStatusCode statusCode = HttpStatusCode.OK) => new(value, statusCode);
    protected static ProblemDetailsResult Problem(ProblemDetails problemDetails) => new(problemDetails);
    protected static FileContentResult File(byte[] fileContent, string contentType, string? fileName = null) => new(fileContent, contentType, fileName);
    protected static FileStreamResult File(Func<Stream> streamFactory, string contentType, string? fileName = null) => new(streamFactory, contentType, fileName);
    protected static SseResult Sse(Func<SseWriter, Task> writer) => new(writer);
}