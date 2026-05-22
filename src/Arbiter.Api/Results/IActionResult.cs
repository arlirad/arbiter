using Arbiter.Api.Http;

namespace Arbiter.Api.Results;

public interface IActionResult
{
    Task ExecuteAsync(HttpContext context);
}
