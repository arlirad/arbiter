using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Application.Mappers;
using Arbiter.Core.Aggregates;

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

        await handleDelegate(transaction, site, context);
        await SendResponse(transaction, context);
    }

    private async Task SendResponse(ITransaction transaction, Context context)
    {
        var response = contextMapper.ToDto(context);

        await transaction.SetResponse(response);
    }
}