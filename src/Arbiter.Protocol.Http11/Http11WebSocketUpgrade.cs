using System.Text;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Protocol.Http11;

internal class Http11WebSocketUpgrade(Stream rawStream, Action onAccept, Func<ValueTask> onUpgradeComplete) : IWebSocketUpgrade
{
    public async Task<Stream> AcceptAsync(ReadOnlyHeaders? responseHeaders = null)
    {
        var sb = new StringBuilder(256);
        sb.Append("HTTP/1.1 101 Switching Protocols\r\n");

        if (responseHeaders is not null)
        {
            foreach (var header in responseHeaders)
            {
                foreach (var value in header.Value)
                {
                    sb.Append(header.Key);
                    sb.Append(": ");
                    sb.Append(value);
                    sb.Append("\r\n");
                }
            }
        }

        sb.Append("\r\n");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await rawStream.WriteAsync(bytes);
        await rawStream.FlushAsync();

        onAccept();

        return new CompletionSignalingStream(rawStream, onUpgradeComplete);
    }
}
