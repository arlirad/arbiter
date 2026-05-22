using System.Text.Json;
using Arbiter.Api.Formatters;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Interfaces;
using Arbiter.Protocol.WebSocket;

namespace Arbiter.Api.Http;

public class HttpContext
{
    private readonly Context _context;

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
        _context = context;
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

    public bool IsUpgrade => _context.Request.Upgrade is not null;
    public bool IsWebSocketUpgrade => _context.Request.Upgrade is IWebSocketUpgrade;

    public async Task<WebSocket?> AcceptWebSocketAsync()
    {
        if (_context.Request.Upgrade is not IWebSocketUpgrade upgrade)
            return null;

        var stream = await upgrade.AcceptAsync();
        _context.IsUpgraded = true;

        var connection = new WebSocketConnection(stream);

        return new WebSocket(connection);
    }
}
