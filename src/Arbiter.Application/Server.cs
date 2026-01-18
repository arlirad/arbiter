using Arbiter.Application.Handlers;
using Arbiter.Application.Interfaces;

namespace Arbiter.Application;

internal class Server(
    IEnumerable<IAcceptor> acceptors,
    IConfigManager configManager,
    TransactionHandler handler
) : IServer
{
    public async Task Run(CancellationToken ct)
    {
        await configManager.CreateDirectories();
        await configManager.InitialConfigure();

        var tasks = acceptors.Select<IAcceptor, Task>(acceptor => Accept(acceptor, ct));

        await Task.WhenAny(tasks);
    }

    private async Task Accept(IAcceptor acceptor, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var transaction = await acceptor.Accept(ct);
                _ = handler.Handle(transaction);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }
}