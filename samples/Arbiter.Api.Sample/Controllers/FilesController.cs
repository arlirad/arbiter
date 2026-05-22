using System.Text;
using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Http;
using Arbiter.Api.Results;

namespace Arbiter.Api.Sample.Controllers;

[Route("api/files")]
public class FilesController : ControllerBase
{
    [HttpGet("{fileName}")]
    public static IActionResult Download(string fileName)
    {
        var fileContent = Encoding.UTF8.GetBytes($"This is content of {fileName}");

        return File(fileContent, "text/plain", fileName);
    }

    [HttpPost("upload")]
    public static IActionResult Upload([FromForm] IFormFile? file)
    {
        if (file is null)
        {
            return BadRequest(new {
                error = "No file provided",
            });
        }

        return Ok(new {
            fileName = file.FileName,
            contentType = file.ContentType,
            size = file.Length,
        });
    }

    [HttpGet("stream")]
    public static IActionResult StreamFile()
    {
        return File(() => {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write("This is a streamed file content");
            writer.Flush();
            stream.Position = 0;

            return stream;
        }, "text/plain", "streamed.txt");
    }

    [HttpGet("error")]
    public static IActionResult ReturnError()
    {
        var problem = new ProblemDetails {
            Type = "https://example.com/probs/not-found",
            Title = "Resource not found",
            Status = 404,
            Detail = "The requested resource could not be found",
            Instance = "/api/files/error",
        };

        return Problem(problem);
    }

    [HttpGet("validation-error")]
    public static IActionResult ValidationError()
    {
        var problem = new ValidationProblemDetails {
            Errors = new Dictionary<string, string[]> {
                ["name"] = ["Name is required"],
                ["email"] = ["Email is not valid"],
            },
            Type = "https://example.com/probs/validation",
            Title = "Validation error",
            Status = 400,
            Detail = "One or more validation errors occurred",
            Instance = "/api/files/validation-error",
        };

        return Problem(problem);
    }
}
