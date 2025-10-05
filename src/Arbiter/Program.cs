using Arbiter;
using Arbiter.Factories;
using Arbiter.Middleware;
using Arbiter.Middleware.Acme;
using Arbiter.Middleware.Static;
using Arbiter.Models.Config;
using Arbiter.Services;
using Arbiter.Services.Configurators;
using Arbiter.Workers;
using Arbiter.Workers.Acme;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
            services.AddSingleton<IConfigurator, SiteManagerConfigurator>();
            services.AddSingleton<IConfigurator, TcpAcceptorConfigurator>();
            services.AddSingleton<IAcceptor, TcpAcceptor>();
            services.AddSingleton<SessionFactory>();
            services.AddSingleton<SiteFactory>();
            services.AddSingleton<Server>();
            services.AddSingleton<Handler>();
            services.AddSingleton<SiteManager>();
            services.AddSingleton<ConfigManager>();
            services.AddSingleton<CertificateManager>();
            services.Configure<ServerConfigModel>(configuration);
            services.AddSingleton<IOptionsMonitor<ServerConfigModel>, OptionsMonitor<ServerConfigModel>>();

            services.AddKeyedScoped<IMiddleware, StaticMiddleware>("static");
            services.AddKeyedScoped<IMiddleware, AcmeMiddleware>("acme");
            services.AddKeyedScoped<IWorker, AcmeWorker>("acme");
            services.AddScoped<MiddlewareChainDelegateFactory>();
            services.AddScoped<MiddlewareFactory>();
            services.AddScoped<WorkerFactory>();

            services.AddTransient<HandleDelegate>(sp =>
            {
                var factory = sp.GetRequiredService<MiddlewareChainDelegateFactory>();
                return factory.GetNext();
            });
        })
        .Build();

    var server = host.Services.GetRequiredService<Server>();
    await server.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Arbiter terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}