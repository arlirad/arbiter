using System.Net;

namespace Arbiter.Api.Results;

public class BadRequestResult : StatusCodeResult
{
    public BadRequestResult() : base(HttpStatusCode.BadRequest)
    {
    }
}
