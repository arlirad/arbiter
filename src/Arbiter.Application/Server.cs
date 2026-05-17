using System.Reactive.Disposables;
using System.Threading;
using Arbiter.Application.Configuration;
using Arbiter.Application.Handlers;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Configuration;
using Arbiter.Core.Factories;
using Arbiter.Core.Interfaces;
using Serilog;

namespace Arbiter.Application;

internal class Server(
    IEnumerable<IAcceptor> acceptors,
    IProtocolFactory protocolFactory,
    SiteManager siteManager,
    IConfigManager configManager,
    ConfigurationProvider configProvider,
    TransactionHandler handler
) : IServer, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];
    private readonly SemaphoreSlim _reconfigureLock = new(1, 1);

    public async Task Run(CancellationToken ct)
    {
        await configManager.CreateDirectories();

        var siteSubscription = configProvider.Observe<Dictionary<string, SiteConfig>>("Sites")
            .Subscribe(async void (sites) => {
                try
                {
                    await _reconfigureLock.WaitAsync();
                    try
                    {
                        await siteManager.ReconfigureAsync(sites);
                    }
                    finally
                    {
                        _reconfigureLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to reconfigure sites");
                }
            });
        _subscriptions.Add(siteSubscription);

        var tasks = acceptors.Select(acceptor => AcceptLoop(acceptor, ct));
        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _reconfigureLock.Dispose();
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
