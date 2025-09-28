using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Arbiter.Infrastructure.Network;
using Arbiter.Models.Config;
using Arbiter.Services;
using Microsoft.Extensions.Options;
using Serilog;

namespace Arbiter;

internal class Server
{
    private readonly Acceptor _acceptor;
    private readonly ConfigManager _configManager;
    private readonly IOptionsMonitor<ServerConfigModel> _configMonitor;
    private readonly Handler _handler;
    private readonly SessionFactory _sessionFactory;
    private readonly SiteManager _siteManager;

    public Server(
        IOptionsMonitor<ServerConfigModel> configMonitor,
        Acceptor acceptor,
        SessionFactory sessionFactory,
        SiteManager siteManager,
        Handler handler,
        ConfigManager configManager
    )
    {
        _configMonitor = configMonitor;
        _acceptor = acceptor;
        _sessionFactory = sessionFactory;
        _siteManager = siteManager;
        _handler = handler;
        _configManager = configManager;
        _configMonitor.OnChange(ConfigChanged);
    }

    public async Task Run()
    {
        await _configManager.CreateDirectories();

        ConfigChanged(_configMonitor.CurrentValue, null);

        while (true)
        {
            var socket = await _acceptor.Accept();
            var session = _sessionFactory.Create(socket);

            _ = Handle(session).ConfigureAwait(false);
        }
    }

    private async Task Handle(Session session)
    {
        while (true)
        {
            var result = await session.Receive();
            await _handler.Handle(result);
        }
    }

    private async void ConfigChanged(ServerConfigModel serverConfig, string? _)
    {
        try
        {
            var (addresses, ports) = ExtractConfigBindings(serverConfig);

            if (addresses is not null && ports is not null)
                await _acceptor.Bind(addresses, ports);

            await _siteManager.Update(serverConfig);
        }
        catch (Exception e)
        {
            Log.Error("Failed to reload config: {Exception}", e);
        }
    }

    /// <summary>
    /// Extracts configuration bindings from the provided ConfigModel.
    /// </summary>
    /// <param name="serverConfig">The configuration model containing binding information.</param>
    /// <returns>A tuple containing lists of IP addresses and ports, or null if listenOn or sites are not set.</returns>
    private static (IEnumerable<IPAddress>? addresses, IEnumerable<int>? ports) ExtractConfigBindings(
        ServerConfigModel serverConfig)
    {
        if (serverConfig.ListenOn is null || serverConfig.Sites is null)
            return (null, null);

        var ports = serverConfig.Sites
            .SelectMany(s => s.Value.Bindings)
            .Select(b => b.Port);

        return (
            serverConfig.ListenOn.Select(IPAddress.Parse),
            ports
        );
    }

    /*private static void UpdateCerts()
    {
        if (!File.Exists("./acme.sh"))
        {
            Console.WriteLine("./acme.sh not found, unable to update certificates");
            return;
        }

        Dictionary<string, Site> certifiable = new Dictionary<string, Site>();

        foreach (var sitePair in Handler.Sites)
        {
            foreach (var binding in sitePair.Value.Bindings)
                if (binding.Scheme == "https" && binding.Port == 443)
                    certifiable[binding.Host] = sitePair.Value;
        }

        foreach (var pair in certifiable)
        {
            if (pair.Key == "localhost")
                continue;

            var host = pair.Key;
            var site = pair.Value;

            Console.WriteLine($"Updating {host}");

            Directory.CreateDirectory("./pfx");
            Process.Start("./acme.sh", $"--issue -d {host} -w {site.Path} --home acme/").WaitForExit();
            Process.Start("openssl", $"pkcs12 -export -out pfx/{host}.pfx -inkey acme/{host}_ecc/{host}.key -in acme/{host}_ecc/fullchain.cer -passout pass:").WaitForExit();
        }
    }*/
}