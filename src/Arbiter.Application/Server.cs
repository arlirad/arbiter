using System.Reactive.Disposables;
using Arbiter.Application.Configuration;
using Arbiter.Application.Handlers;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Serilog;

namespace Arbiter.Application;

internal class Server(
    TransportManager transportManager,
    IProtocolFactory protocolFactory,
    SiteManager siteManager,
    IConfigManager configManager,
    ISitesProvider sitesProvider,
    TransactionHandler handler
) : IServer, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "server");
    private readonly SemaphoreSlim _reconfigureLock = new(1, 1);
    private readonly CompositeDisposable _subscriptions = [];

    public void Dispose()
    {
        _subscriptions.Dispose();
        _reconfigureLock.Dispose();
    }

    public async Task Run(CancellationToken ct)
    {
        await configManager.CreateDirectories();

        transportManager.Initialize();

        var siteSubscription = sitesProvider.ObserveSites()
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

        _subscriptions.Add(transportManager.NewAcceptor.Subscribe(acceptor => _ = AcceptLoop(acceptor, ct)));

        foreach (var acceptor in transportManager.ActiveAcceptors)
            _ = AcceptLoop(acceptor, ct);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
        }
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
