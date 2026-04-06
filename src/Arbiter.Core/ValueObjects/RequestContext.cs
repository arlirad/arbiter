using System.Net;
using Arbiter.Core.Enums;

namespace Arbiter.Core.ValueObjects;

public class RequestContext
{
    internal RequestContext(
        int transactionId,
        Method method,
        string path,
        Headers headers,
        Stream? stream,
        bool isWebSocketUpgrade,
        string? authority,
        bool isSecure,
        IPAddress? remoteAddress)
    {
        TransactionId = transactionId;
        Method = method;
        Path = path;
        Headers = new ReadOnlyHeaders(headers);
        Stream = stream;
        IsWebSocketUpgrade = isWebSocketUpgrade;
        Authority = authority;
        IsSecure = isSecure;
        RemoteAddress = remoteAddress;
    }

    public int TransactionId { get; }
    public Method Method { get; }
    public string Path { get; set; }
    public ReadOnlyHeaders Headers { get; }
    public Stream? Stream { get; private set; }
    public bool IsWebSocketUpgrade { get; }
    public string? Authority { get; }
    public bool IsSecure { get; }
    public IPAddress? RemoteAddress { get; }
}