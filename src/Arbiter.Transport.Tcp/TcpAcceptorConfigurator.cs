using System.Net;
using Arbiter.Models.Config;
using Arbiter.Models.Config.Sites;
using Arbiter.Services;
using Arbiter.Services.Configurators;
using Arbiter.Transport.Abstractions;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Transport.Tcp;

public class TcpAcceptorConfigurator(IEnumerable<IAcceptor> acceptors) : IConfigurator
{
    public async Task Configure(IConfiguration serverConfig)
    {
        try
        {
            var (addresses, ports) = ExtractConfigBindings(serverConfig);

            if (addresses is not null && ports is not null)
                if (acceptors.FirstOrDefault(a => a.GetType() == typeof(TcpAcceptor)) is TcpAcceptor acceptor)
                    await acceptor.Bind(addresses, ports);
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
        IConfiguration serverConfig)
    {
        var listenOn = serverConfig.GetSection("ListenOn").Get<List<string>>();
        var sites = serverConfig.GetSection("Sites").Get<Dictionary<string, SiteConfigModel>>();

        if (listenOn is null || sites is null)
            return (null, null);

        var ports = sites
            .SelectMany(s => s.Value.Bindings ?? [])
            .Select(b => b.Port);

        return (
            listenOn.Select(IPAddress.Parse),
            ports
        );
    }
}