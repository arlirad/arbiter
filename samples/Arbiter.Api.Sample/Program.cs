using Arbiter.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

var services = builder.Services;

var apiBuilder = ApiBuilder.Create(services)
    .ConfigureJson(options => {
        options.WriteIndented = true;
        options.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    })
    .WithControllers()
    .UseRequestLogging()
    .UseHttpsRedirection(443)
    .UseRateLimiting(maxRequests: 100, windowSeconds: 60)
    .UseHealthChecks();

var api = apiBuilder.Build();

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, _) => {
    Log.Information("Shutting down...");
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