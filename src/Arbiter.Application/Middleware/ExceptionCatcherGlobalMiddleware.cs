using Arbiter.Application.Interfaces;
using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Enums;

namespace Arbiter.Application.Middleware;

public class ExceptionCatcherGlobalMiddleware(HandleDelegate next) : IGlobalMiddleware
{
    public async Task Handle(ITransaction transaction, Site? site, Context context)
    {
        try
        {
            await next(transaction, site, context);
        }
        catch (Exception e)
        {
#if DEBUG
            await context.Response.Set(Status.InternalServerError, await CreateExceptionPage(e));
#else
            await context.Response.Set(Status.InternalServerError);
#endif
        }
    }

    private static async ValueTask<Stream> CreateExceptionPage(Exception exception)
    {
        var ms = new MemoryStream();

        await using (var writer = new StreamWriter(ms, leaveOpen: true))
        {
            await writer.WriteLineAsync("<html><title>500 - Internal Server Error</title></html>");
            await writer.WriteLineAsync("<body>");
            await writer.WriteLineAsync("<h1>500 - Internal Server Error</h1>");
            await writer.WriteLineAsync(exception.ToString().ReplaceLineEndings("<br>"));
            await writer.WriteLineAsync("</body>");
        }

        ms.Position = 0;

        return ms;
    }
}