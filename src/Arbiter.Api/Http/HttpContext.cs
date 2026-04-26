using System.Text.Json;
using Arbiter.Api.Formatters;
using Arbiter.Core.Aggregates;

namespace Arbiter.Api.Http;

public class HttpContext
{
    private Stream? _webSocketStream;

    internal HttpContext(Context context, IServiceProvider requestServices, string? queryString, CancellationToken cancellationToken, JsonSerializerOptions? jsonOptions = null, OutputFormatterSelector? outputFormatterSelector = null)
    {
        var query = QueryCollection.Parse(queryString);

        Request = new HttpRequest(context.Request, query);
        Response = new HttpResponse(context.Response);
        RequestServices = requestServices;
        CancellationToken = cancellationToken;
        JsonSerializerOptions = jsonOptions ?? new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
        };
        OutputFormatterSelector = outputFormatterSelector;
    }

    public HttpRequest Request
    {
        get;
    }
    public HttpResponse Response
    {
        get;
    }
    public IServiceProvider RequestServices
    {
        get;
        internal set;
    }
    public CancellationToken CancellationToken
    {
        get;
    }
    public AuthResult? AuthInfo
    {
        get;
        internal set;
    }
    public JsonSerializerOptions JsonSerializerOptions
    {
        get;
    }
    public OutputFormatterSelector? OutputFormatterSelector
    {
        get;
    }

    public bool IsWebSocketUpgrade => Request.Headers["Upgrade"]?.Equals("websocket", StringComparison.OrdinalIgnoreCase) == true;

    public Task<WebSocket?> AcceptWebSocketAsync()
    {
        _webSocketStream = Request.Body;
        return Task.FromResult<WebSocket?>(_webSocketStream is null ? null : new WebSocket(_webSocketStream, isServer: true));
    }
}