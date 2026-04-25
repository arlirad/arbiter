using Arbiter.Core.ValueObjects;

namespace Arbiter.Api.Http;

public class RequestHeaders
{
    private readonly ReadOnlyHeaders _inner;

    internal RequestHeaders(ReadOnlyHeaders inner)
    {
        _inner = inner;
    }

    public string? this[string key] => _inner[key]?.FirstOrDefault();

    public string? Authorization => this["Authorization"];

    public string? ContentType => this["Content-Type"];

    public string? Accept => this["Accept"];

    public string? UserAgent => this["User-Agent"];

    public string? Host => this["Host"];
}