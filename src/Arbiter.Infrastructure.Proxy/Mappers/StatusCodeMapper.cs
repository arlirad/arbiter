using System.Net;
using Arbiter.Domain.Enums;

namespace Arbiter.Infrastructure.Proxy.Mappers;

public static class StatusCodeMapper
{
    private static readonly Dictionary<HttpStatusCode, Status> Codes = new()
    {
        [HttpStatusCode.Continue] = Status.Continue,
        [HttpStatusCode.SwitchingProtocols] = Status.SwitchingProtocol,
        [HttpStatusCode.EarlyHints] = Status.EarlyHints,
        [HttpStatusCode.OK] = Status.Ok,
        [HttpStatusCode.Created] = Status.Created,
        [HttpStatusCode.Accepted] = Status.Accepted,
        [HttpStatusCode.NonAuthoritativeInformation] = Status.NonAuthoritativeInformation,
        [HttpStatusCode.NoContent] = Status.NoContent,
        [HttpStatusCode.ResetContent] = Status.ResetContent,
        [HttpStatusCode.PartialContent] = Status.PartialContent,
        [HttpStatusCode.MultipleChoices] = Status.MultipleChoice,
        [HttpStatusCode.MovedPermanently] = Status.MovedPermanently,
        [HttpStatusCode.Found] = Status.Found,
        [HttpStatusCode.SeeOther] = Status.SeeOther,
        [HttpStatusCode.NotModified] = Status.NotModified,
        [HttpStatusCode.UseProxy] = Status.UseProxy,
        [HttpStatusCode.TemporaryRedirect] = Status.TemporaryRedirect,
        [HttpStatusCode.PermanentRedirect] = Status.PermanentRedirect,
        [HttpStatusCode.BadRequest] = Status.BadRequest,
        [HttpStatusCode.Unauthorized] = Status.Unauthorized,
        [HttpStatusCode.PaymentRequired] = Status.PaymentRequired,
        [HttpStatusCode.Forbidden] = Status.Forbidden,
        [HttpStatusCode.NotFound] = Status.NotFound,
        [HttpStatusCode.MethodNotAllowed] = Status.MethodNotAllowed,
        [HttpStatusCode.NotAcceptable] = Status.NotAcceptable,
        [HttpStatusCode.ProxyAuthenticationRequired] = Status.ProxyAuthenticationRequired,
        [HttpStatusCode.RequestTimeout] = Status.RequestTimeout,
        [HttpStatusCode.Conflict] = Status.Conflict,
        [HttpStatusCode.Gone] = Status.Gone,
        [HttpStatusCode.LengthRequired] = Status.LengthRequired,
        [HttpStatusCode.PreconditionFailed] = Status.PreconditionFailed,
        [HttpStatusCode.RequestUriTooLong] = Status.UriTooLong,
        [HttpStatusCode.UnsupportedMediaType] = Status.UnsupportedMediaType,
        [HttpStatusCode.RequestedRangeNotSatisfiable] = Status.RangeNotSatisfiable,
        [HttpStatusCode.ExpectationFailed] = Status.ExpectationFailed,
        [HttpStatusCode.MisdirectedRequest] = Status.MisdirectedRequest,
        [HttpStatusCode.UnprocessableEntity] = Status.UnprocessableEntity,
        [HttpStatusCode.Locked] = Status.Locked,
        [HttpStatusCode.FailedDependency] = Status.FailedDependency,
        [HttpStatusCode.UpgradeRequired] = Status.UpgradeRequired,
        [HttpStatusCode.PreconditionRequired] = Status.PreconditionRequired,
        [HttpStatusCode.TooManyRequests] = Status.TooManyRequests,
        [HttpStatusCode.RequestHeaderFieldsTooLarge] = Status.RequestHeaderFieldsTooLarge,
        [HttpStatusCode.UnavailableForLegalReasons] = Status.UnavailableForLegalReasons,
        [HttpStatusCode.InternalServerError] = Status.InternalServerError,
        [HttpStatusCode.NotImplemented] = Status.NotImplemented,
        [HttpStatusCode.BadGateway] = Status.BadGateway,
        [HttpStatusCode.ServiceUnavailable] = Status.ServiceUnavailable,
        [HttpStatusCode.GatewayTimeout] = Status.GatewayTimeout,
        [HttpStatusCode.HttpVersionNotSupported] = Status.VersionNotSupported,
        [HttpStatusCode.VariantAlsoNegotiates] = Status.VariantAlsoNegotiates,
        [HttpStatusCode.InsufficientStorage] = Status.InsufficientStorage,
        [HttpStatusCode.LoopDetected] = Status.LoopDetected,
        [HttpStatusCode.NotExtended] = Status.NotExtended,
        [HttpStatusCode.NetworkAuthenticationRequired] = Status.NetworkAuthenticationRequired,
    };

    public static Status? FromHttpStatusCode(HttpStatusCode httpStatusCode)
    {
        return Codes.TryGetValue(httpStatusCode, out var status)
            ? status
            : null;
    }
}