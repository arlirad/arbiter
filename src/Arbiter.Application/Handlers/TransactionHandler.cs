using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Application.Mappers;
using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Enums;

namespace Arbiter.Application.Handlers;

internal class TransactionHandler(
    SiteManager siteManager,
    ContextMapper contextMapper
)
{
    public async Task Handle(ITransaction transaction)
    {
        var request = await transaction.GetRequest();

        if (request is null)
            return;

        var context = contextMapper.ToDomain(request);
        var site = siteManager.Find(request.Authority, transaction.Port);

        if (site is null)
        {
            await context.Response.Set(Status.NotFound);
            await SendResponse(transaction, context);

            return;
        }

        await site.HandleDelegate(context);
        await SendResponse(transaction, context);
    }

    private async Task SendResponse(ITransaction transaction, Context context)
    {
        var response = contextMapper.ToDto(context);

        await transaction.SetResponse(response);
    }
}