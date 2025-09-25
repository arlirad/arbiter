namespace Arbiter.DTOs;

internal class HttpResponse(HttpVersion version)
{
    public HttpVersion Version { get; } = version;
    public HttpHeaders Headers { get; } = new();
}