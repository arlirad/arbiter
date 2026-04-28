using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Application.Mappers;
using Arbiter.Core.Aggregates;
using Serilog;

namespace Arbiter.Application.Handlers;

internal class TransactionHandler(SiteManager siteManager, ContextMapper contextMapper, HandleDelegate handleDelegate)
{
    public async Task Handle(ITransaction transaction)
    {
        var request = await transaction.GetRequest();

        if (request is null)
            return;

        var context = contextMapper.ToDomain(request);

        if (context is null)
            return;

        var site = transaction.Port == -1
            ? siteManager.Find(request.Authority)
            : siteManager.Find(request.Authority, transaction.Port);

        await Handle(transaction, site, context, request);

        if (!context.IsUpgraded)
            await SendResponse(transaction, context);
    }

    private async Task Handle(ITransaction transaction, Site? site, Context context, RequestDto request)
    {

        var handleTask = handleDelegate(transaction, site, context);

        if (!handleTask.IsCompleted)
        {
            Log.Information("{remoteAddress} ({id}) >> {method} {authority}:{path}",
                request.RemoteAddress, request.TransactionId, request.Method, request.Authority, request.Path);

            await handleTask;

            Log.Information("{remoteAddress} ({id}) << {status}",
                request.RemoteAddress, request.TransactionId, context.Response.Status);
        }
        else
        {
            await handleTask;
            Log.Information("{remoteAddress} ({id}) >> {method} {authority}:{path} << {status}",
                request.RemoteAddress, request.TransactionId, request.Method, request.Authority, request.Path, context.Response.Status);
        }
    }

    private async Task SendResponse(ITransaction transaction, Context context)
    {
        var response = contextMapper.ToDto(context);

        await transaction.SetResponse(response);
    }
}