namespace Arbiter.Api.Results;

public class NotFoundResult : StatusCodeResult
{
    public NotFoundResult() : base(System.Net.HttpStatusCode.NotFound)
    {
    }
}