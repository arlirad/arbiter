using Arbiter.Domain.Enums;

namespace Arbiter.Infrastructure.Mappers;

public static class MethodMapper
{
    public static Method? ToEnum(string method)
    {
        return method switch
        {
            "GET" => Method.Get,
            "HEAD" => Method.Head,
            "OPTIONS" => Method.Options,
            "TRACE" => Method.Trace,
            "PUT" => Method.Put,
            "DELETE" => Method.Delete,
            "POST" => Method.Post,
            "PATCH" => Method.Patch,
            _ => null,
        };
    }

    public static string? ToString(Method method)
    {
        return method switch
        {
            Method.Get => "GET",
            Method.Head => "HEAD",
            Method.Options => "OPTIONS",
            Method.Trace => "TRACE",
            Method.Put => "PUT",
            Method.Delete => "DELETE",
            Method.Post => "POST",
            Method.Patch => "PATCH",
            _ => null,
        };
    }
}