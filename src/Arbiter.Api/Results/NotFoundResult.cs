using System.Net;

namespace Arbiter.Api.Results;

public class NotFoundResult : StatusCodeResult
{
    public NotFoundResult() : base(HttpStatusCode.NotFound)
    {
    }
}
