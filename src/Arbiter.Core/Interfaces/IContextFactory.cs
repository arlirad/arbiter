using System.Net;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;

namespace Arbiter.Core.Interfaces;

public interface IContextFactory
{
    Context? Create(
        int transactionId,
        Method method,
        string path,
        IEnumerable<KeyValuePair<string, List<string>>> headers,
        Stream? stream,
        IUpgrade? upgrade,
        string? authority,
        bool isSecure,
        IPAddress? remoteAddress);
}
