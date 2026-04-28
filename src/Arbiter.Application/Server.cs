using Arbiter.Application.Handlers;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Application.Mappers;
using Arbiter.Application.Orchestrators;
using Arbiter.Core.Factories;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Application;

internal class Server(
    IEnumerable<IAcceptor> acceptors,
    IProtocolFactory protocolFactory,
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

        var tasks = acceptors.Select<IAcceptor, Task>(acceptor => AcceptLoop(acceptor, ct));

        await Task.WhenAll(tasks);
    }

    private async Task AcceptLoop(IAcceptor acceptor, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var transport = await acceptor.Accept(ct);
                _ = HandleConnection(transport, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }

    private async Task HandleConnection(ITransport transport, CancellationToken ct)
    {
        try
        {
            await using var protocol = protocolFactory.Create(transport.Protocol);
            await foreach (var transaction in protocol.AcceptTransactions(transport, ct))
                _ = HandleWithLogging(transaction);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.Error(e, "Error handling connection");
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