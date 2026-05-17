using Arbiter.Application.DTOs;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Application.Mappers;

internal class ContextMapper(IContextFactory contextFactory)
{
    public Context? ToDomain(RequestDto request)
    {
        var context = contextFactory.Create(request.TransactionId, request.Method, request.Path, request.Headers,
            request.Stream, request.Upgrade, request.Authority, request.IsSecure, request.RemoteAddress);

        return context;
    }

    public static ResponseDto ToDto(Context context)
    {
        return new ResponseDto() {
            Status = context.Response.Status!.Value,
            Headers = new ReadOnlyHeaders(context.Response.Headers),
            Stream = context.Response.Stream,
        };
    }
}
