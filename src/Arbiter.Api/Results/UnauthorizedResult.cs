using System.Net;

namespace Arbiter.Api.Results;

public class UnauthorizedResult() : StatusCodeResult(HttpStatusCode.Unauthorized);
