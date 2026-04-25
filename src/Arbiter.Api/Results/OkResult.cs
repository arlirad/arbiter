namespace Arbiter.Api.Results;

public class OkResult : StatusCodeResult
{
    public OkResult() : base(System.Net.HttpStatusCode.OK)
    {
    }
}