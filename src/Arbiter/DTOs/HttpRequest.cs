namespace Arbiter.DTOs;

internal class HttpRequest(
    HttpMethod method,
    string path,
    HttpVersion version,
    HttpHeaders headers
)
{
    public HttpMethod Method { get; } = method;
    public string Path { get; } = path;
    public HttpVersion Version { get; } = version;
    public HttpHeaders Headers { get; } = headers;
}