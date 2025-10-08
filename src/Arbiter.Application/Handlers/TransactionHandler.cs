using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Arbiter.Application.Mappers;
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
            return;
        }

        await site.HandleDelegate(context);

        var response = contextMapper.ToDto(context);

        await transaction.SetResponse(response);
    }
}