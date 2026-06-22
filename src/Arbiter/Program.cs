using System.Net.Quic;
using System.Reflection;
using Arbiter.Application;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Constants;
using Arbiter.Infrastructure;
using Arbiter.Infrastructure.Acme;
using Arbiter.Infrastructure.Cors;
using Arbiter.Infrastructure.Headers;
using Arbiter.Infrastructure.Headers.Managers;
using Arbiter.Infrastructure.Proxy;
using Arbiter.Infrastructure.Rewriting;
using Arbiter.Protocol.Http11;
using Arbiter.Protocol.Http3;
using Arbiter.Transport.Quic;
using Arbiter.Transport.Tcp;
using Arbiter.Transport.Unix;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var log = Log.ForContext("SourceContext", AppConstants.Name.ToLowerInvariant());

var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0];
log.Information("Starting {Name}/{Version}", AppConstants.Name, version);

try
{
    using var host = Host
        .CreateDefaultBuilder(args)
        .ConfigureServices((context, services) => {
            services.AddConfiguration(args);
            services.AddHttp11Protocol();

            if (QuicListener.IsSupported)
                services.AddHttp3Protocol();

            services.AddTcpTransport();
            services.AddQuicTransport();
            services.AddUnixSocketTransport();
            services.AddInfrastructure(args);
            services.AddAcmeInfrastructure();
            services.AddCorsInfrastructure();
            services.AddProxyInfrastructure();
            services.AddRewritingInfrastructure();
            services.AddApplication();
            services.AddHeadersInfrastructure();
        })
        .Build();

    var sp = host.Services;
    sp.GetRequiredService<GlobalHeadersManager>();

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
