using System.ComponentModel.DataAnnotations;
using System.Net;
using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Results;

namespace Arbiter.Api.Sample.Controllers;

[Route("api/users")]
public class UsersController : ControllerBase
{
    [HttpGet]
    public static IActionResult GetAll()
    {
        var users = new[] {
            new {
                Id = 1,
                Name = "Alice",
                Email = "alice@example.com",
            },
            new {
                Id = 2,
                Name = "Bob",
                Email = "bob@example.com",
            },
        };

        return Ok(users);
    }

    [HttpGet("{id}")]
    public static IActionResult GetById(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new {
                error = "Invalid user ID",
            });
        }

        return Ok(new {
            Id = id,
            Name = "User " + id,
            Email = $"user{id}@example.com",
        });
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateUserRequest request)
    {
        return ModelState.IsValid
            ? Created($"/api/users/{request.Name}", new {
                Id = 3,
                request.Name,
                request.Email,
            })
            : BadRequest(ModelState);
    }

    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] UpdateUserRequest request)
    {
        return ModelState.IsValid
            ? Ok(new {
                Id = id,
                request.Name,
                request.Email,
            })
            : BadRequest(ModelState);
    }

    [HttpDelete("{id}")]
    public static IActionResult Delete(int id) => NoContent();

    [HttpHead("{id}")]
    public IActionResult Head(int id)
    {
        HttpContext.Response.StatusCode = 200;

        return StatusCode(HttpStatusCode.OK);
    }

    [HttpOptions]
    public IActionResult Options()
    {
        HttpContext.Response.Headers["Allow"] = "GET, POST, PUT, DELETE, HEAD, OPTIONS";

        return StatusCode(HttpStatusCode.OK);
    }
}

public class CreateUserRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string Name
    {
        get;
        set;
    } = null!;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email is not valid")]
    public string Email
    {
        get;
        set;
    } = null!;
}

public class UpdateUserRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string Name
    {
        get;
        set;
    } = null!;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email is not valid")]
    public string Email
    {
        get;
        set;
    } = null!;
}
