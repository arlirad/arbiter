using System.Net;

namespace Arbiter.Api.Results;

public class OkResult : StatusCodeResult
{
    public OkResult() : base(HttpStatusCode.OK)
    {
    }
}
