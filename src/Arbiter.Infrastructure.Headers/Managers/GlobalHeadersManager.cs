using System.Reactive.Disposables;
using Arbiter.Application.Configuration;
using Arbiter.Application.Middleware;
using Arbiter.Configuration;
using Serilog;

namespace Arbiter.Infrastructure.Headers.Managers;

public sealed class GlobalHeadersManager : IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "headers");
    private readonly CompositeDisposable _subscriptions = [];

    public GlobalHeadersManager(ConfigurationProvider configProvider, GlobalMiddlewareChain chain, IServiceProvider serviceProvider)
    {
        var subscription = configProvider.Observe<ServerHeadersConfig>("headers")
            .Subscribe(headersConfig => {
                try
                {
                    chain.Build(serviceProvider);
                    Log.Information("Header configuration: Server={Server}, Date={Date}, RequestId={RequestId}",
                        headersConfig.Server, headersConfig.Date, headersConfig.RequestId);

                    if (headersConfig.StrictTransportSecurity is { } hsts)
                        Log.Information("Strict-Transport-Security: MaxAge={MaxAge}, IncludeSubDomains={IncludeSubDomains}, Preload={Preload}",
                            hsts.MaxAge, hsts.IncludeSubDomains, hsts.Preload);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to rebuild global middleware chain with Server={Server}, Date={Date}, RequestId={RequestId}",
                        headersConfig.Server, headersConfig.Date, headersConfig.RequestId);
                }
            });

        _subscriptions.Add(subscription);
    }

    public void Dispose() => _subscriptions.Dispose();
}
