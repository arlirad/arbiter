internal class SessionReceiveResult
{
    public HttpRequest? Request { get; }
    public bool IsBad { get; }
    public bool IsClosed { get; }

    private SessionReceiveResult(HttpRequest? request = null, bool bad = false, bool closed = false)
    {
        Request = request;
        IsBad = bad;
        IsClosed = closed;
    }

    public static SessionReceiveResult Ok(HttpRequest request) => new(request: request);

    public static SessionReceiveResult BadRequest() => new(bad: true);

    public static SessionReceiveResult Closed() => new(closed: true);
}