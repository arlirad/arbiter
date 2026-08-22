using System.Net;
using System.Text.Json;
using Arbiter.Api.Formatters;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Middleware;
using Arbiter.Protocol.Http11;
using Arbiter.Transport.Tcp;
using Arbiter.Transport.Unix;
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
            var unixTransport = new UnixSocketTransport();
            await unixTransport.Bind([builder.UnixSocketPath], 128);
            Log.Information("API listening on unix://{Path}", builder.UnixSocketPath);

            while (!ct.IsCancellationRequested)
            {
                var connection = await unixTransport.Accept(ct);
                _ = HandleConnection(connection, ct);
            }
        }
        else
        {
            var addresses = builder.Addresses.Count > 0
                ? builder.Addresses
                : [IPAddress.Any];

            var tcpTransport = new TcpTransport(certificateManager);
            await tcpTransport.Bind(addresses, [builder.Port], 128);
            Log.Information("API listening on {Addresses}:{Port}", string.Join(", ", addresses), builder.Port);

            while (!ct.IsCancellationRequested)
            {
                var connection = await tcpTransport.Accept(ct);
                _ = HandleConnection(connection, ct);
            }
        }
    }

    private async Task HandleConnection(IConnection connection, CancellationToken ct)
    {
        await using var protocol = new Http11Protocol(new TransactionIdProvider());

        await foreach (var transaction in protocol.AcceptTransactions(connection, ct))
            _ = HandleTransaction(transaction, ct);
    }

    private async Task<Site> BuildSite()
    {
        var (pipelineMiddleware, apiMiddleware) = await BuildMiddlewareChainAsync();

        var allMiddleware = pipelineMiddleware.Concat([apiMiddleware]).ToList();

        HandleDelegate entryHandle = pipelineMiddleware.Count > 0
            ? pipelineMiddleware[0].Handle
            : apiMiddleware.Handle;

        var site = new Site(
            [],
            allMiddleware,
            [],
            entryHandle
        );

        for (var i = 0; i < builder.MiddlewareEntries.Count; i++)
        {
            var configure = builder.MiddlewareEntries[i].Configure;
            if (configure is not null)
                await configure(pipelineMiddleware[i], site.Data);
        }

        return site;
    }

    private async Task<(List<IMiddleware> PipelineMiddleware, ApiMiddleware ApiMiddleware)> BuildMiddlewareChainAsync()
    {
        var orchestrator = new MiddlewareChainOrchestrator();

        static Task terminal(Context _) => Task.CompletedTask;
        orchestrator.SetNext(terminal);

        var apiMiddleware = new ApiMiddleware(
            builder.ControllerTypes,
            serviceProvider,
            serviceProvider.GetRequiredService<JsonSerializerOptions>(),
            serviceProvider.GetRequiredService<OutputFormatterSelector>());
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

    private async Task HandleTransaction(ITransaction transaction, CancellationToken ct)
    {
        Context? context = null;

        try
        {
            var request = await transaction.GetRequest(ct);

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

            await SendResponse(transaction, context, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling API request");

            try
            {
                if (context is not null)
                {
                    await context.Response.Set(Status.InternalServerError);
                    await SendResponse(transaction, context, ct);
                }
            }
            catch
            {
            }
        }
    }

    private static async Task SendResponse(ITransaction transaction, Context context, CancellationToken ct)
    {
        var response = new ResponseDto {
            Status = context.Response.Status ?? Status.Ok,
            Headers = context.Response.Headers,
            Stream = context.Response.Stream,
        };

        await transaction.SetResponse(response, ct);
    }

    private sealed class MiddlewareChainServiceProvider(IServiceProvider innerProvider, MiddlewareChainOrchestrator orchestrator) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(HandleDelegate) ? orchestrator.GetNext() : innerProvider.GetService(serviceType);
    }
}
