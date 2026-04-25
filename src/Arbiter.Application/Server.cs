using Arbiter.Application.Handlers;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Application;

internal class Server(
    IEnumerable<IAcceptor> acceptors,
    SiteManager siteManager,
    IConfigManager configManager,
    IConfiguration configuration,
    TransactionHandler handler
) : IServer
{
    public async Task Run(CancellationToken ct)
    {
        await configManager.CreateDirectories();
        await siteManager.Bind(configuration);

        foreach (var acceptor in acceptors.OfType<IAsyncConfigurable>())
            await acceptor.Bind(configuration);

        var tasks = acceptors.Select<IAcceptor, Task>(acceptor => Accept(acceptor, ct));

        await Task.WhenAll(tasks);
    }

    private async Task Accept(IAcceptor acceptor, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var transaction = await acceptor.Accept(ct);
                _ = HandleWithLogging(transaction);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }

    private async Task HandleWithLogging(ITransaction transaction)
    {
        try
        {
            await handler.Handle(transaction);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error handling transaction {TransactionId}", transaction.Id);
        }
    }
}