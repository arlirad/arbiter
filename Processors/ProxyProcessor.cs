using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Arbiter;

[Name("proxyproc")]
public class ProxyProcessor : IProcessor
{
    public void Process(Stream stream, Request request, Response response)
    {
        var destination = request.Site.Parameters["proxy_destination"] ?? throw new NullReferenceException("Parameter 'proxy_destination' is undefined.");
        var ep = IPEndPoint.Parse(destination);

        response.Proxy(request, ep, request.RewrittenUri.AbsolutePath);
    }
}