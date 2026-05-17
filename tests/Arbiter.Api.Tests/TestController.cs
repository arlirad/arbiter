using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Results;

namespace Arbiter.Api.Tests;

[Route("api/users")]
public class TestController : IApiController
{
    [HttpGet]
    public static IActionResult GetAll() => new Results.OkResult();

    [HttpGet("{id:int}")]
    public static IActionResult GetById(int id) => new Results.OkResult();

    [HttpPost]
    public static IActionResult Create() => new Results.CreatedResult("/api/users/1", null);

    [HttpPut("{id}")]
    public static IActionResult Update(int id) => new Results.OkResult();

    [HttpDelete("{id}")]
    public static IActionResult Delete(int id) => new Results.NoContentResult();

    [HttpGet("search")]
    public static IActionResult Search() => new Results.OkResult();

    [HttpGet("{id}/items/{itemId}")]
    public static IActionResult GetItem(int id, int itemId) => new Results.OkResult();

    [HttpGet("optional/{id?}")]
    public static IActionResult GetOptional(int? id) => new Results.OkResult();

    [HttpGet("files/{**path}")]
    public static IActionResult GetFiles(string path) => new Results.OkResult();
}