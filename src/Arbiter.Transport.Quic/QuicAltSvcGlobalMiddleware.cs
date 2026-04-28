using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Arbiter.Transport.Quic;

internal class QuicAltSvcGlobalMiddleware : IGlobalMiddleware, IConfigurable
{
    private readonly HandleDelegate _next;
    private List<int> _quicPorts = [];
    private ConfigurationScope? _scope;

    public QuicAltSvcGlobalMiddleware(
        HandleDelegate next,
        IConfiguration configuration)
    {
        _next = next;
        Bind(configuration);
    }

    public void Bind(IConfiguration configuration)
    {
        _scope = new ConfigurationScope(configuration, "QuicPorts");
        UpdatePorts();
        ChangeToken.OnChange(_scope.GetReloadToken, UpdatePorts);
    }

    public Task Handle(ITransaction transaction, Site? site, Context context)
    {
        if (site is null || transaction.Protocol == Protocol.Http3)
            return _next(transaction, site, context);

        var port = site.Bindings
            .Where(b => _quicPorts.Any(qp => qp == b.Port))
            .Select(b => b.Port)
            .OrderBy(p => p)
            .ToList();

        if (port.Count == 0)
            return _next(transaction, site, context);

        context.Response.Headers.AltSvc = $"h3=\":{port.First()}\"; ma=86400";
        return _next(transaction, site, context);
    }

    private void UpdatePorts()
    {
        var quicPorts = _scope?.GetSection("QuicPorts").Get<List<int>>();

        if (quicPorts != null)
            _quicPorts = [.. quicPorts];
    }
}