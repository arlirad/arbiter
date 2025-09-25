using Arbiter.DTOs;

namespace Arbiter.Models.Network;

internal class HttpRequestContext(HttpRequest request)
{
    public HttpMethod Method => request.Method;
    public string Uri => request.Uri;
    public HttpVersion Version => request.Version;
    public HttpHeaders Headers => request.Headers;
}