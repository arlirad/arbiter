using System.Net;
using Arbiter.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Transport.Quic;

internal class QuicAcceptorConfigurator(IEnumerable<IAcceptor> acceptors) : IConfigurator
{
    public async Task Configure(IConfiguration serverConfig)
    {
        try
        {
            var endpoints = ExtractConfigBindings(serverConfig);

            if (endpoints is not null)
                if (acceptors.FirstOrDefault(a => a.GetType() == typeof(QuicAcceptor)) is QuicAcceptor acceptor)
                    await acceptor.Bind(endpoints);
        }
        catch (Exception e)
        {
            Log.Error("Failed to reload config: {Exception}", e);
        }
    }

    private static IEnumerable<IPEndPoint> ExtractConfigBindings(IConfiguration serverConfig)
    {
        var listenOn = serverConfig.GetSection("ListenOn").Get<List<string>>();
        var quicPorts = serverConfig.GetSection("QuicPorts").Get<List<int>>();

        if (listenOn is null || quicPorts is null)
            return null;

        var ports = quicPorts;
        var endpoints = new List<IPEndPoint>();

        foreach (var address in listenOn)
        {
            foreach (var port in ports)
            {
                endpoints.Add(new IPEndPoint(IPAddress.Parse(address), port));
            }
        }

        return endpoints;
    }
}