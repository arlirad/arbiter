namespace Arbiter.Api.Results;

public class NoContentResult : StatusCodeResult
{
    public NoContentResult() : base(System.Net.HttpStatusCode.NoContent)
    {
    }
}