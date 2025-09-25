using Arbiter.Infrastructure.Network;
using Arbiter.Models.Network;

internal class Handler
{
    public async Task Handle(SessionReceiveResult result)
    {
        if (result.Request is null)
            throw new Exception("Handle SessionReceiveResult.Request is null");

        if (result.Stream is null)
            throw new Exception("Handle SessionReceiveResult.Stream is null");

        var requestContext = new HttpRequestContext(result.Request);
        var responseContext = new HttpResponseContext(result.Request.Version, result.Stream);

        await responseContext.Send(HttpStatusCode.NotImplemented, new MemoryStream());
    }
}