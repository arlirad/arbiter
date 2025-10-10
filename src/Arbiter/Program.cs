using Arbiter.Application;
using Arbiter.Application.Interfaces;
using Arbiter.Infrastructure;
using Arbiter.Infrastructure.Acme;
using Arbiter.Transport.Tcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var configPath = args.Any(s => s == "--local-config")
    ? Path.Combine(Directory.GetCurrentDirectory(), "./cfg/arbiter.json")
    : "/etc/arbiter/arbiter.json";

var configuration = new ConfigurationBuilder()
    .AddJsonFile(configPath, optional: false, reloadOnChange: true)
    .Build();

Log.Information("Starting Arbiter");

try
{
    using var host = Host
        .CreateDefaultBuilder(args)
        .ConfigureServices((_, services) =>
        {
            services.AddSingleton<IConfiguration>(configuration);

            services.AddTcpTransport();
            services.AddInfrastructure();
            services.AddAcmeInfrastructure();
            services.AddApplication();
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