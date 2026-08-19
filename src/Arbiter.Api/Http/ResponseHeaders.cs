using Arbiter.Core.ValueObjects;

namespace Arbiter.Api.Http;

public class ResponseHeaders
{
    private readonly ResponseContext _response;

    internal ResponseHeaders(ResponseContext response)
    {
        _response = response;
    }

    public string? this[string key]
    {
        get => _response.Headers[key]?.FirstOrDefault();
        set {
            if (value is null)
                _response.RemoveHeader(key);
            else
                _response.SetHeader(key, value);
        }
    }
}
