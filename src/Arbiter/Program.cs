using Arbiter;
using Arbiter.Factories;
using Arbiter.Middleware;
using Arbiter.Middleware.Acme;
using Arbiter.Middleware.Static;
using Arbiter.Models.Config;
using Arbiter.Services;
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

var middlewareRegistry = new ComponentTypeRegistry<IMiddleware>(new Dictionary<string, Type>()
{
    ["static"] = typeof(StaticMiddleware),
    ["acme"] = typeof(AcmeMiddleware),
});

var workerRegistry = new ComponentTypeRegistry<IWorker>(new Dictionary<string, Type>()
{
    ["acme"] = typeof(AcmeWorker),
});

Log.Information("Starting Arbiter");

try
{
    using var host = Host
        .CreateDefaultBuilder(args)
        .ConfigureServices((_, services) =>
        {
            services.AddSingleton<SessionFactory>();
            services.AddSingleton<SiteFactory>();
            services.AddSingleton<Server>();
            services.AddSingleton<Acceptor>();
            services.AddSingleton<Handler>();
            services.AddSingleton<MiddlewareFactory>();
            services.AddSingleton<WorkerFactory>();
            services.AddSingleton<SiteManager>();
            services.Configure<ConfigModel>(configuration);
            services.AddSingleton<IOptionsMonitor<ConfigModel>, OptionsMonitor<ConfigModel>>();
            services.AddSingleton(middlewareRegistry);
            services.AddSingleton(workerRegistry);
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