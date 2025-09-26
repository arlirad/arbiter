using Arbiter.DTOs;

namespace Arbiter.Models.Network;

internal class HttpRequestContext(HttpRequest request, Uri uri, Site site)
{
    public HttpMethod Method => request.Method;
    public Uri Uri => uri;
    public HttpVersion Version => request.Version;
    public HttpHeaders Headers => request.Headers;
    public Site Site => site;
}