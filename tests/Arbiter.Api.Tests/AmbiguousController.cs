using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Results;

namespace Arbiter.Api.Tests;

[Route("api/ambiguous")]
public class AmbiguousController : IApiController
{
    [HttpGet("{id}")]
    public IActionResult GetById(int id) => new Results.OkResult();

    [HttpGet("{id}")]
    public IActionResult GetByIdDuplicate(int id) => new Results.OkResult();
}