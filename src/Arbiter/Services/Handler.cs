using Arbiter.Enums;
using Arbiter.Infrastructure.Network;
using Arbiter.Models.Network;
using Serilog;

namespace Arbiter.Services;

internal class Handler(SiteManager siteManager)
{
    public async Task Handle(SessionReceiveResult result)
    {
        if (result.Request is null)
            throw new Exception("Handle SessionReceiveResult.Request is null");

        if (result.Stream is null)
            throw new Exception("Handle SessionReceiveResult.Stream is null");

        if (result.Uri is null)
            throw new Exception("Handle SessionReceiveResult.Uri is null");
        
        var site = siteManager.Find(result.Uri);
        var responseContext = new HttpResponseContext(result.Request.Version, result.Stream);
        
        if (site is null)
        {
            await responseContext.Send(HttpStatusCode.NotFound, Stream.Null);
            return;
        }
        
        var requestContext = new HttpRequestContext(result.Request, result.Uri, site);

        try
        {
            var handled = false;

            foreach (var middleware in site.Middlewares)
            {
                if (!await middleware.CanHandle(requestContext))
                    continue;

                await middleware.Handle(requestContext, responseContext);
                handled = true;
                
                break;
            }

            if (!handled)
                await responseContext.Send(HttpStatusCode.InternalServerError);
        }
        catch (Exception e)
        {
            Log.Error("Exception caught while handling a request for site '{Site}': {Exception}", site, e);
            await responseContext.Send(HttpStatusCode.InternalServerError, Stream.Null);
        }
    }
}