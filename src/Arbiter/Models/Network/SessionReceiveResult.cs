using Arbiter.DTOs;

namespace Arbiter.Infrastructure.Network;

internal class SessionReceiveResult
{
    public HttpRequest? Request { get; }
    public bool IsBad { get; }
    public bool IsClosed { get; }
    public bool IsSecure { get; }
    public Stream? Stream { get; }
    public Uri? Uri { get; }
    public int Port { get; }

    private SessionReceiveResult(
        HttpRequest? request = null,
        Stream? stream = null,
        Uri? uri = null,
        bool isSecure = false,
        bool bad = false,
        bool closed = false,
        int port = 0
    )
    {
        Request = request;
        Stream = stream;
        Uri = uri;
        IsBad = bad;
        IsClosed = closed;
        IsSecure = isSecure;
        Port = port;
    }

    public static SessionReceiveResult Ok(HttpRequest request, Stream stream, bool isSecure, int port) => 
        new(
            request: request, 
            stream: stream,
            isSecure: isSecure,
            port: port,
            uri: ConstructUri(request, isSecure, port)
        );

    public static SessionReceiveResult BadRequest() => new(bad: true);

    public static SessionReceiveResult Closed() => new(closed: true);

    private static Uri ConstructUri(HttpRequest request, bool isSecure, int port)
    {
        var host = request.Version == HttpVersion.Http11 
            ? request.Headers["Host"]!.Split(':')[0]
            : "*";
        
        return new Uri($"{(isSecure ? "https" : "http")}://{host}:{port}/{request.Path}");
    }
}