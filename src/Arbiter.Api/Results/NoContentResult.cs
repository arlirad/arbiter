using System.Net;

namespace Arbiter.Api.Results;

public class NoContentResult : StatusCodeResult
{
    public NoContentResult() : base(HttpStatusCode.NoContent)
    {
    }
}
