using Arbiter.Core.ValueObjects;

namespace Arbiter.Api.Http;

public class ResponseHeaders
{
    private readonly Headers _inner;

    internal ResponseHeaders(Headers inner)
    {
        _inner = inner;
    }

    public string? this[string key]
    {
        get => _inner[key]?.FirstOrDefault();
        set => _inner[key] = value is not null ? [value] : null;
    }
}
