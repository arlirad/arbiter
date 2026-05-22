using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;

namespace Arbiter.Infrastructure.Headers;

public class StrictTransportSecurityGlobalMiddleware : IGlobalMiddleware
{
    private readonly StrictTransportSecurityConfig _config;
    private readonly HandleDelegate _next;

    public StrictTransportSecurityGlobalMiddleware(HandleDelegate next, StrictTransportSecurityConfig config)
    {
        _next = next;
        _config = config;

        if (_config.Preload && !_config.IncludeSubDomains)
            throw new InvalidOperationException("StrictTransportSecurity: preload requires includeSubDomains to be enabled.");

        if (_config.Preload && _config.MaxAge < 31536000)
            throw new InvalidOperationException("StrictTransportSecurity: preload requires maxAge of at least 31536000 seconds (1 year).");
    }

    public async Task Handle(ITransaction transaction, Site? site, Context context)
    {
        if (transaction.IsSecure)
        {
            var value = $"max-age={_config.MaxAge}";

            if (_config.IncludeSubDomains)
                value += "; includeSubDomains";

            if (_config.Preload)
                value += "; preload";

            context.Response.Headers.Add("Strict-Transport-Security", value);
        }

        await _next(transaction, site, context);
    }
}
