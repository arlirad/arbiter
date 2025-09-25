using Arbiter.DTOs;

namespace Arbiter.Infrastructure.Network;

internal class SessionReceiveResult
{
    public HttpRequest? Request { get; }
    public bool IsBad { get; }
    public bool IsClosed { get; }
    public Stream? Stream { get; }

    private SessionReceiveResult(
        HttpRequest? request = null,
        Stream? stream = null,
        bool bad = false,
        bool closed = false
    )
    {
        Request = request;
        Stream = stream;
        IsBad = bad;
        IsClosed = closed;
    }

    public static SessionReceiveResult Ok(HttpRequest request, Stream stream) => new(request: request, stream: stream);

    public static SessionReceiveResult BadRequest() => new(bad: true);

    public static SessionReceiveResult Closed() => new(closed: true);
}