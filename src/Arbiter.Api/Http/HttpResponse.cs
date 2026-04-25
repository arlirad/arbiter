using System.Text;
using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Api.Http;

public class HttpResponse
{
    private readonly ResponseContext _response;
    private MemoryStream? _body;

    internal HttpResponse(ResponseContext response)
    {
        _response = response;
        Headers = new ResponseHeaders(response.Headers);
    }

    public int StatusCode
    {
        get;
        set;
    } = 200;

    public string? ContentType
    {
        get => _response.Headers.ContentType;
        set => _response.Headers.ContentType = value;
    }

    public ResponseHeaders Headers
    {
        get;
    }

    public Task WriteAsync(string text, Encoding? encoding = null)
    {
        _body ??= new MemoryStream();
        var bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
        _body.Write(bytes);
        return Task.CompletedTask;
    }

    public Task WriteAsync(byte[] data)
    {
        _body ??= new MemoryStream();
        _body.Write(data);
        return Task.CompletedTask;
    }

    internal void Apply()
    {
        _body?.Position = 0;

        _response.Set((Status)StatusCode, _body);
    }
}