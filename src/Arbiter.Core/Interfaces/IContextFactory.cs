using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;

namespace Arbiter.Core.Interfaces;

public interface IContextFactory
{
    Context? Create(
        Method method,
        string path,
        IEnumerable<KeyValuePair<string, List<string>>> headers,
        Stream? stream,
        bool isWebSocketUpgrade);
}