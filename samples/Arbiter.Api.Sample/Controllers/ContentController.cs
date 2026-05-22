using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Results;

namespace Arbiter.Api.Sample.Controllers;

[Route("api/content")]
public class ContentController : ControllerBase
{
    [HttpGet("negotiate")]
    public static IActionResult ContentNegotiation()
    {
        var data = new {
            Message = "Hello, world!",
            Timestamp = DateTime.UtcNow,
            Numbers = new[] {
                1, 2, 3, 4, 5,
            },
        };

        return Ok(data);
    }

    [HttpGet("text")]
    public static IActionResult PlainText() => Ok("This is plain text content");

    [HttpGet("json")]
    public static IActionResult JsonContent()
    {
        return Json(new {
            Type = "json",
            Message = "This is JSON content",
            Timestamp = DateTime.UtcNow,
        });
    }

    [HttpGet("custom")]
    public IActionResult CustomContentType()
    {
        HttpContext.Response.ContentType = "application/vnd.api+json";

        return Ok(new {
            Type = "custom",
            Message = "This is custom content type",
        });
    }
}
