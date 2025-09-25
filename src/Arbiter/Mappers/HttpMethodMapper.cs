namespace Arbiter.Mappers;

internal static class HttpMethodMapper
{
    public static HttpMethod? ToEnum(string method)
    {
        return method switch
        {
            "GET" => HttpMethod.Get,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            "TRACE" => HttpMethod.Trace,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "POST" => HttpMethod.Post,
            "PATCH" => HttpMethod.Patch,
            _ => null,
        };
    }

    public static string? ToString(HttpMethod method)
    {
        return method switch
        {
            HttpMethod.Get => "GET",
            HttpMethod.Head => "HEAD",
            HttpMethod.Options => "OPTIONS",
            HttpMethod.Trace => "TRACE",
            HttpMethod.Put => "PUT",
            HttpMethod.Delete => "DELETE",
            HttpMethod.Post => "POST",
            HttpMethod.Patch => "PATCH",
            _ => null,
        };
    }
}