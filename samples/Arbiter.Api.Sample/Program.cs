using System.Text.Json;
using Arbiter.Api;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var log = Log.ForContext("SourceContext", "arbiter");

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

var services = builder.Services;

var apiBuilder = ApiBuilder.Create(services)
    .ConfigureJson(options => {
        options.WriteIndented = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    })
    .WithControllers()
    .UseRequestLogging()
    .UseHttpsRedirection()
    .UseRateLimiting(100, 60)
    .UseHealthChecks();

var api = apiBuilder.Build();

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, _) => {
    log.Information("Shutting down...");
    cts.Cancel();
};

try
{
    await api.Run(cts.Token);
}
catch (OperationCanceledException)
{
}
finally
{
    Log.CloseAndFlush();
}
