using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Enums;

namespace Arbiter.Domain.Interfaces;

public interface IContextFactory
{
    Context? Create(
        Method method,
        string path,
        IEnumerable<KeyValuePair<string, List<string>>> headers,
        Stream? stream);
}