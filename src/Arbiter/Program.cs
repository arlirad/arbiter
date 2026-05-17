using System.Reflection;
using Arbiter.Application;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Constants;
using Arbiter.Infrastructure;
using Arbiter.Infrastructure.Acme;
using Arbiter.Infrastructure.Cors;
using Arbiter.Infrastructure.Proxy;
using Arbiter.Infrastructure.Rewriting;
using Arbiter.Transport.Quic;
using Arbiter.Transport.Tcp;
using Arbiter.Transport.Unix;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0];
Log.Information("Starting {Name}/{Version}", AppConstants.Name, version);

try
{
    using var host = Host
        .CreateDefaultBuilder(args)
        .ConfigureServices((_, services) => {
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

            services.AddApplicationGlobalMiddleware();
            services.AddQuicGlobalMiddleware();
        })
        .Build();

    var server = host.Services.GetRequiredService<IServer>();
    await server.Run(CancellationToken.None);
}
catch (Exception ex)
{
    Log.Fatal(ex, "{Name} terminated unexpectedly", AppConstants.Name);
}
finally
{
    Log.CloseAndFlush();
}