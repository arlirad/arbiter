using Arbiter.Application.DTOs;
using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Interfaces;
using Arbiter.Domain.ValueObjects;

namespace Arbiter.Application.Mappers;

internal class ContextMapper(IContextFactory contextFactory)
{
    public Context? ToDomain(RequestDto request)
    {
        var context = contextFactory.Create(request.Method, request.Path, request.Headers, request.Stream);
        return context;
    }

    public ResponseDto ToDto(Context context)
    {
        return new ResponseDto()
        {
            Status = context.Response.Status!.Value,
            Headers = new ReadOnlyHeaders(context.Response.Headers),
            Stream = context.Response.Stream,
        };
    }
}