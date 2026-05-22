using System.Net;
using System.Text.Json;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Api.Http;

public class HttpRequest
{
    private readonly RequestContext _request;

    internal HttpRequest(RequestContext request, QueryCollection query)
    {
        _request = request;

        var fullPath = request.Path;
        var queryIndex = fullPath.IndexOf('?');
        Path = queryIndex >= 0 ? fullPath[..queryIndex] : fullPath;
        QueryString = queryIndex >= 0 ? fullPath[queryIndex..] : null;
        Method = request.Method.ToString().ToUpperInvariant();
        Body = request.Stream;
        Query = query;
        Headers = new RequestHeaders(request.Headers);
        RemoteAddress = request.RemoteAddress;
        IsSecure = request.IsSecure;
    }

    public string Method
    {
        get;
    }
    public string Path
    {
        get;
    }
    public string? QueryString
    {
        get;
    }
    public Stream? Body
    {
        get;
    }
    public QueryCollection Query
    {
        get;
    }
    public RouteValueDictionary RouteValues
    {
        get;
        internal set;
    } = RouteValueDictionary.Empty;
    public RequestHeaders Headers
    {
        get;
    }
    public IPAddress? RemoteAddress
    {
        get;
    }
    public bool IsSecure
    {
        get;
    }

    public async ValueTask<T?> ReadFromJsonAsync<T>(JsonSerializerOptions? options = null) => Body is null || !Body.CanRead ? default : await JsonSerializer.DeserializeAsync<T>(Body, options);
}
