using System.Net;
using Arbiter.Models.Config;
using Serilog;

namespace Arbiter.Services.Configurators;

internal class TcpAcceptorConfigurator(IEnumerable<IAcceptor> acceptors) : IConfigurator
{
    public async Task Configure(ServerConfigModel serverConfig)
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
}