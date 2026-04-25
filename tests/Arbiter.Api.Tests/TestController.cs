using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Results;

namespace Arbiter.Api.Tests;

[Route("api/users")]
public class TestController : IApiController
{
    [HttpGet]
    public IActionResult GetAll() => new Results.OkResult();

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id) => new Results.OkResult();

    [HttpPost]
    public IActionResult Create() => new Results.CreatedResult("/api/users/1", null);

    [HttpPut("{id}")]
    public IActionResult Update(int id) => new Results.OkResult();

    [HttpDelete("{id}")]
    public IActionResult Delete(int id) => new Results.NoContentResult();

    [HttpGet("search")]
    public IActionResult Search() => new Results.OkResult();

    [HttpGet("{id}/items/{itemId}")]
    public IActionResult GetItem(int id, int itemId) => new Results.OkResult();

    [HttpGet("optional/{id?}")]
    public IActionResult GetOptional(int? id) => new Results.OkResult();

    [HttpGet("files/{**path}")]
    public IActionResult GetFiles(string path) => new Results.OkResult();
}