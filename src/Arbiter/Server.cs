using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Arbiter.Infrastructure.Network;
using Arbiter.Models.Config;
using Arbiter.Services;
using Microsoft.Extensions.Options;

namespace Arbiter;

internal class Server
{
    private readonly Acceptor _acceptor;
    private readonly SessionFactory _sessionFactory;
    private readonly Receiver _handler;
    private readonly IOptionsMonitor<ConfigModel> _configMonitor;

    public Server(
        IOptionsMonitor<ConfigModel> configMonitor,
        Acceptor acceptor,
        SessionFactory sessionFactory,
        Receiver handler)
    {
        _acceptor = acceptor;
        _sessionFactory = sessionFactory;
        _handler = handler;
        _configMonitor = configMonitor;
        _configMonitor.OnChange(ConfigChanged);
    }

    public async Task Run()
    {
        ConfigChanged(_configMonitor.CurrentValue, null);

        while (true)
        {
            var socket = await _acceptor.Accept();
            var session = _sessionFactory.Create(socket);

            _ = _handler.Receive(session);
        }
    }

    private async void ConfigChanged(ConfigModel config, string? _)
    {
        var (addresses, ports) = ExtractConfigBindings(config);

        if (addresses is not null && ports is not null)
            await _acceptor.Bind(addresses, ports);
    }

    private static (IEnumerable<IPAddress>? addresses, IEnumerable<int>? ports) ExtractConfigBindings(ConfigModel config)
    {
        if (config.ListenOn is null || config.Sites is null)
            return (null, null);

        var ports = config.Sites
            .SelectMany(s => s.Value.Bindings)
            .Select(b => b.Port);

        return (
            config.ListenOn.Select(b => IPAddress.Parse(b)),
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