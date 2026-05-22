using System.Runtime.Versioning;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;
using Arbiter.Protocol.Http3.Streams;

namespace Arbiter.Protocol.Http3;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
internal class H3WebSocketUpgrade(Http3RequestStream requestStream) : IWebSocketUpgrade
{
    public async Task<Stream> AcceptAsync(ReadOnlyHeaders? responseHeaders = null)
    {
        var headers = new Dictionary<string, List<string>> {
            [":status"] = ["200"],
        }.AsEnumerable();

        if (responseHeaders is not null)
            headers = headers.Concat(responseHeaders);

        await requestStream.WriteHeaders(headers);
        await requestStream.FlushAsync();
        requestStream.MarkAsUpgrade();

        return requestStream;
    }
}
