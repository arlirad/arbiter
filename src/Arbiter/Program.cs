using Arbiter;
using Arbiter.Models.Config;
using Arbiter.Services;
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
            services.AddSingleton<Server>();
            services.AddSingleton<Acceptor>();
            services.AddSingleton<SessionFactory>();
            services.AddSingleton<Receiver>();
            services.AddSingleton<Handler>();
            services.Configure<ConfigModel>(configuration);
            services.AddSingleton<IOptionsMonitor<ConfigModel>, OptionsMonitor<ConfigModel>>();
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