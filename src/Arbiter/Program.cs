using Arbiter;
using Arbiter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using var host = Host
    .CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<Server>();
        services.AddSingleton<Acceptor>();
        services.AddSingleton<SessionFactory>();
    })
    .Build();

var server = host.Services.GetRequiredService<Server>();
await server.Run();