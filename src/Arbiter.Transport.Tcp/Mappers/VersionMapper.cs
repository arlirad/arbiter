using Arbiter.Transport.Tcp.Enums;

namespace Arbiter.Transport.Tcp.Mappers;

internal static class VersionMapper
{
    public static HttpVersion? ToEnum(string version)
    {
        return version switch
        {
            "HTTP/1.0" => HttpVersion.Http10,
            "HTTP/1.1" => HttpVersion.Http11,
            _ => null,
        };
    }

    public static string? ToString(HttpVersion version)
    {
        return version switch
        {
            HttpVersion.Http10 => "HTTP/1.0",
            HttpVersion.Http11 => "HTTP/1.1",
            _ => null,
        };
    }
}