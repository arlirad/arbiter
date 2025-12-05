using Arbiter.Domain.Enums;

namespace Arbiter.Infrastructure.Proxy.Mappers;

public static class MethodMapper
{
    public static HttpMethod ToHttpMethod(Method method)
    {
        return method switch
        {
            Method.Get => HttpMethod.Get,
            Method.Head => HttpMethod.Head,
            Method.Options => HttpMethod.Options,
            Method.Trace => HttpMethod.Trace,
            Method.Put => HttpMethod.Put,
            Method.Delete => HttpMethod.Delete,
            Method.Post => HttpMethod.Post,
            Method.Patch => HttpMethod.Patch,
            Method.Connect => HttpMethod.Connect,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
        };
    }
}