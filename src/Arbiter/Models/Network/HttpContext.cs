namespace Arbiter.Models.Network;

internal class HttpContext(HttpRequestContext request, HttpResponseContext response)
{
    public HttpRequestContext Request { get => request; }
    public HttpResponseContext Response { get => response; }
}