using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Results;

namespace Arbiter.Api.Tests;

[Route("api/ambiguous")]
public class AmbiguousController : IApiController
{
    [HttpGet("{id}")]
    public static IActionResult GetById(int id) => new OkResult();

    [HttpGet("{id}")]
    public static IActionResult GetByIdDuplicate(int id) => new OkResult();
}
