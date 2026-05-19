using System.Reflection;
using Arbiter.Application;
using Arbiter.Application.Configuration;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Middleware;
using Arbiter.Core.Constants;
using Arbiter.Infrastructure;
using Arbiter.Infrastructure.Acme;
using Arbiter.Infrastructure.Cors;
using Arbiter.Infrastructure.Headers;
using Arbiter.Infrastructure.Proxy;
using Arbiter.Infrastructure.Rewriting;
using Arbiter.Transport.Quic;
using Arbiter.Transport.Tcp;
using Arbiter.Transport.Unix;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var log = Log.ForContext("SourceContext", "arbiter");

var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0];
log.Information("Starting {Name}/{Version}", AppConstants.Name, version);

try
{
    using var host = Host
        .CreateDefaultBuilder(args)
        .ConfigureServices((context, services) => {
            services.AddConfiguration(args);
            services.AddTcpTransport();
            services.AddQuicTransport();
            services.AddUnixSocketTransport();
            services.AddSingleton<IProtocolFactory, ProtocolFactory>();
            services.AddInfrastructure();
            services.AddAcmeInfrastructure();
            services.AddCorsInfrastructure();
            services.AddProxyInfrastructure();
            services.AddRewritingInfrastructure();
            services.AddApplication();
            services.AddSingleton<Action<ServerHeadersConfig, GlobalMiddlewareChain>>((hc, c) => {
                Arbiter.Application.DependencyInjection.ConfigureGlobalMiddlewareChain(c);
                c.ConfigureHeaderMiddleware(hc);
            });
        })
        .Build();

    var sp = host.Services;
    var config = sp.GetRequiredService<IConfiguration>();
    var headersConfig = config.GetSection("headers").Get<ServerHeadersConfig>() ?? new ServerHeadersConfig();

    var chain = sp.GetRequiredService<GlobalMiddlewareChain>();
    Arbiter.Application.DependencyInjection.ConfigureGlobalMiddlewareChain(chain);
    chain.ConfigureHeaderMiddleware(headersConfig);
    chain.Build(sp);

    var server = sp.GetRequiredService<IServer>();
    await server.Run(CancellationToken.None);
}
catch (Exception ex)
{
    log.Fatal(ex, "{Name} terminated unexpectedly", AppConstants.Name);
}
finally
{
    Log.CloseAndFlush();
}
