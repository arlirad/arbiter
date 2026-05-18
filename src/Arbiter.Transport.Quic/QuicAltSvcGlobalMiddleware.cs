using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;

namespace Arbiter.Transport.Quic;

internal class QuicAltSvcGlobalMiddleware(HandleDelegate next, QuicPortService quicPortService) : IGlobalMiddleware
{
    private readonly HandleDelegate _next = next;
    private readonly QuicPortService _quicPortService = quicPortService;

    public Task Handle(ITransaction transaction, Site? site, Context context)
    {
        if (site is null || transaction.Protocol == Protocol.Http3)
            return _next(transaction, site, context);

        if (!_quicPortService.Announce)
            return _next(transaction, site, context);

        var port = site.Bindings
            .Where(b => _quicPortService.Ports.Any(qp => qp == b.Port))
            .Select(b => b.Port)
            .OrderBy(p => p)
            .FirstOrDefault();

        if (port == 0)
            return _next(transaction, site, context);

        context.Response.Headers.AltSvc = $"h3=\":{port}\"; ma=86400";
        return _next(transaction, site, context);
    }
}