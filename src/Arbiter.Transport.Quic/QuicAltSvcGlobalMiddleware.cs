using Arbiter.Application.Interfaces;
using Arbiter.Domain.Aggregates;

namespace Arbiter.Transport.Quic;

internal class QuicAltSvcGlobalMiddleware(HandleDelegate next) : IGlobalMiddleware
{
    private List<int> _quicPorts = [];

    public Task Handle(ITransaction transaction, Site? site, Context context)
    {
        if (site is null || transaction is QuicTransaction)
            return next(transaction, site, context);

        var port = site.Bindings
            .Where(b => _quicPorts.Any(qp => qp == b.Port))
            .Select(b => b.Port)
            .OrderBy(p => p)
            .ToList();

        if (port.Count == 0)
            return next(transaction, site, context);

        context.Response.Headers.AltSvc = $"h3=\":{port.First()}\"; ma=86400";
        return next(transaction, site, context);
    }

    public void SetPorts(List<int> quicPorts)
    {
        _quicPorts = [.. quicPorts];
    }
}