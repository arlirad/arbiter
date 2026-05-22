using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Results;

namespace Arbiter.Api.Sample.Controllers;

[Route("api/routing")]
public class RoutingController : ControllerBase
{
    [HttpGet("optional/{id?}")]
    public static IActionResult OptionalParameter(int? id)
    {
        if (id.HasValue)
        {
            return Ok(new {
                message = $"ID is {id}",
            });
        }

        return Ok(new {
            message = "No ID provided",
        });
    }

    [HttpGet("files/{**path}")]
    public static IActionResult CatchAll(string path)
    {
        return Ok(new {
            message = "Catch-all path matched",
            path,
            segments = path.Split('/'),
        });
    }

    [HttpGet("constrained/{id:int}")]
    public static IActionResult IntConstraint(int id)
    {
        return Ok(new {
            message = $"Integer ID is {id}",
            type = "int",
        });
    }

    [HttpGet("constrained/{id:guid}")]
    public static IActionResult GuidConstraint(Guid id)
    {
        return Ok(new {
            message = $"GUID ID is {id}",
            type = "guid",
        });
    }

    [HttpGet("search")]
    public static IActionResult SearchQuery([FromQuery] string? q, [FromQuery] int? page = 1)
    {
        return Ok(new {
            query = q ?? "no query",
            page,
            message = q is null ? "No search query provided" : $"Searching for: {q}",
        });
    }

    [HttpGet("multi-value")]
    public static IActionResult MultiValueQuery([FromQuery] string[] tags)
    {
        return Ok(new {
            tags,
            count = tags.Length,
        });
    }
}
