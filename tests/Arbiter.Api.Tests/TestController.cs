using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Results;

namespace Arbiter.Api.Tests;

[Route("api/users")]
public class TestController : IApiController
{
    [HttpGet]
    public static IActionResult GetAll() => new OkResult();

    [HttpGet("{id:int}")]
    public static IActionResult GetById(int id) => new OkResult();

    [HttpPost]
    public static IActionResult Create() => new CreatedResult("/api/users/1", null);

    [HttpPut("{id}")]
    public static IActionResult Update(int id) => new OkResult();

    [HttpDelete("{id}")]
    public static IActionResult Delete(int id) => new NoContentResult();

    [HttpGet("search")]
    public static IActionResult Search() => new OkResult();

    [HttpGet("{id}/items/{itemId}")]
    public static IActionResult GetItem(int id, int itemId) => new OkResult();

    [HttpGet("optional/{id?}")]
    public static IActionResult GetOptional(int? id) => new OkResult();

    [HttpGet("files/{**path}")]
    public static IActionResult GetFiles(string path) => new OkResult();
}
