using Arbiter.Application;
using Arbiter.Application.Interfaces;
using Arbiter.Infrastructure;
using Arbiter.Infrastructure.Acme;
using Arbiter.Infrastructure.Cors;
using Arbiter.Infrastructure.Proxy;
using Arbiter.Transport.Quic;
using Arbiter.Transport.Tcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Starting Arbiter");

try
{
    using var host = Host
        .CreateDefaultBuilder(args)
        .ConfigureServices((_, services) =>
        {
            services.AddConfiguration(args);
            services.AddTcpTransport();
            services.AddQuicTransport();
            services.AddInfrastructure();
            services.AddAcmeInfrastructure();
            services.AddCorsInfrastructure();
            services.AddProxyInfrastructure();
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
    Log.Fatal(ex, "Arbiter terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}