using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Arbiter.Factories;
using Arbiter.Infrastructure.Network;
using Arbiter.Models.Config;
using Arbiter.Services;
using Arbiter.Transport.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;

namespace Arbiter;

internal class Server(
    IEnumerable<IAcceptor> acceptors,
    SessionFactory sessionFactory,
    SiteManager siteManager,
    Handler handler,
    ConfigManager configManager
)
{
    public async Task Run()
    {
        await configManager.CreateDirectories();
        await configManager.InitialConfigure();

        while (true)
        {
            var socket = await acceptors.First().Accept();
            var session = sessionFactory.Create(socket);

            _ = Handle(session).ConfigureAwait(false);
        }
    }

    private async Task Handle(Session session)
    {
        while (true)
        {
            var result = await session.Receive();
            await handler.Handle(result);
        }
    }
}