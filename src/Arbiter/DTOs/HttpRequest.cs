namespace Arbiter.DTOs;

internal class HttpRequest(
    HttpMethod method,
    string uri,
    HttpVersion version,
    HttpHeaders headers
)
{
    public HttpMethod Method { get; } = method;
    public string Uri { get; } = uri;
    public HttpVersion Version { get; } = version;
    public HttpHeaders Headers { get; } = headers;
}