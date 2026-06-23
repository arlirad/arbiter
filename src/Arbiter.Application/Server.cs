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

        _subscriptions.Add(transportManager.NewTransport.Subscribe(transport => _ = AcceptLoop(transport, ct)));

        foreach (var transport in transportManager.ActiveTransports)
            _ = AcceptLoop(transport, ct);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task AcceptLoop(ITransport transport, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var connection = await transport.Accept(ct);
                _ = HandleConnection(connection, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HandleConnection(IConnection connection, CancellationToken ct)
    {
        try
        {
            await using var protocol = protocolFactory.Create(connection.Protocol);
            await foreach (var transaction in protocol.AcceptTransactions(connection, ct))
                _ = HandleWithLogging(transaction, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Log.Error(e, "Error handling connection");
        }
    }

    private async Task HandleWithLogging(ITransaction transaction, CancellationToken ct)
    {
        try
        {
            await handler.Handle(transaction, ct);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error handling transaction {TransactionId}", transaction.Id);
        }
    }
}
