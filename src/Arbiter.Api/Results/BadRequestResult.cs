namespace Arbiter.Api.Results;

public class BadRequestResult : StatusCodeResult
{
    public BadRequestResult() : base(System.Net.HttpStatusCode.BadRequest)
    {
    }
}