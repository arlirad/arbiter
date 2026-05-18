using System.Net;
using Arbiter.Api.Http;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;
using Arbiter.Protocol.Http11;
using Arbiter.Transport.Tcp;
using Arbiter.Transport.Unix;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using HandleDelegate = Arbiter.Core.Interfaces.HandleDelegate;

namespace Arbiter.Api;

internal sealed class Api(
    ApiBuilder builder,
    IContextFactory contextFactory,
    ICertificateManager certificateManager,
    IServiceProvider serviceProvider
) : IApi
{
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "api");
    private Site? _site;

    public async Task Run(CancellationToken ct)
    {
        _site = await BuildSite();

        if (!string.IsNullOrEmpty(builder.UnixSocketPath))
        {
            var unixAcceptor = new UnixSocketAcceptor();
            await unixAcceptor.Bind([builder.UnixSocketPath], 128);
            Log.Information("API listening on unix://{Path}", builder.UnixSocketPath);

            while (!ct.IsCancellationRequested)
            {
                var transport = await unixAcceptor.Accept(ct);
                _ = HandleConnection(transport, ct);
            }
        }
        else
        {
            var addresses = builder.Addresses.Count > 0
                ? builder.Addresses
                : [IPAddress.Any];

            var tcpAcceptor = new TcpAcceptor(certificateManager);
            await tcpAcceptor.Bind(addresses, [builder.Port], 128);
            Log.Information("API listening on {Addresses}:{Port}", string.Join(", ", addresses), builder.Port);

            while (!ct.IsCancellationRequested)
            {
                var transport = await tcpAcceptor.Accept(ct);
                _ = HandleConnection(transport, ct);
            }
        }
    }

    private async Task HandleConnection(ITransport transport, CancellationToken ct)
    {
        await using var protocol = new Http11Protocol();

        await foreach (var transaction in protocol.AcceptTransactions(transport, ct))
            _ = HandleTransaction(transaction, ct);
    }

    private async Task<Site> BuildSite()

    {
        var emptyConfig = new ConfigurationBuilder().Build();

        var (pipelineMiddleware, apiMiddleware) = await BuildMiddlewareChainAsync();

        var allMiddleware = pipelineMiddleware.Concat([apiMiddleware]).ToList();

        HandleDelegate entryHandle = pipelineMiddleware.Count > 0
            ? pipelineMiddleware[0].Handle
            : apiMiddleware.Handle;

        var handleDelegate = new HandleDelegate(entryHandle);

        var site = new Site(
            "api",
            [],
            allMiddleware,
            [],
            handleDelegate
        );

        for (var i = 0; i < builder.MiddlewareEntries.Count; i++)
        {
            var config = builder.MiddlewareEntries[i].Config ?? emptyConfig;
            await pipelineMiddleware[i].Configure(site.Path, site.Data, config);
        }

        await apiMiddleware.Configure(site.Path, site.Data, emptyConfig);

        return site;
    }

    private async Task<(List<IMiddleware> PipelineMiddleware, ApiMiddleware ApiMiddleware)> BuildMiddlewareChainAsync()
    {
        var orchestrator = new MiddlewareChainOrchestrator();

        static Task terminal(Context _) => Task.CompletedTask;
        orchestrator.SetNext(terminal);

        var apiMiddleware = new ApiMiddleware(builder.ControllerTypes, serviceProvider);
        orchestrator.SetNext(apiMiddleware.Handle);

        var pipelineMiddlewareTypes = builder.MiddlewareEntries
            .Select(e => e.Type)
            .ToList();

        var pipelineMiddlewareTypesReversed = pipelineMiddlewareTypes.ToList();
        pipelineMiddlewareTypesReversed.Reverse();

        var resolvedMiddlewares = new List<IMiddleware>();

        foreach (var type in pipelineMiddlewareTypesReversed)
        {
            using var scope = serviceProvider.CreateScope();

            var customProvider = new MiddlewareChainServiceProvider(scope.ServiceProvider, orchestrator);

            var middleware = (IMiddleware)ActivatorUtilities.CreateInstance(customProvider, type);

            resolvedMiddlewares.Add(middleware);

            orchestrator.SetNext(middleware.Handle);
        }

        resolvedMiddlewares.Reverse();

        return (resolvedMiddlewares, apiMiddleware);
    }

    private async Task HandleTransaction(ITransaction transaction, CancellationToken _)
    {
        Context? context = null;

        try
        {
            var request = await transaction.GetRequest();
            if (request is null)
                return;

            context = contextFactory.Create(
                request.TransactionId,
                request.Method,
                request.Path,
                request.Headers,
                request.Stream,
                request.Upgrade,
                request.Authority,
                request.IsSecure,
                request.RemoteAddress);

            if (context is null)
                return;

            await _site!.HandleDelegate(context);

            await SendResponse(transaction, context);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling API request");

            try
            {
                if (context is not null)
                {
                    await context.Response.Set(Status.InternalServerError);
                    await SendResponse(transaction, context);
                }
            }
            catch
            {
                // Ignored
            }
        }
    }

    private static async Task SendResponse(ITransaction transaction, Context context)
    {
        var response = new ResponseDto {
            Status = context.Response.Status ?? Status.Ok,
            Headers = new ReadOnlyHeaders(context.Response.Headers),
            Stream = context.Response.Stream,
        };

        await transaction.SetResponse(response);
    }

    private sealed class MiddlewareChainServiceProvider(IServiceProvider innerProvider, MiddlewareChainOrchestrator orchestrator) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(HandleDelegate) ? orchestrator.GetNext() : innerProvider.GetService(serviceType);
    }
}
