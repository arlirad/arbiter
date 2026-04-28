using Arbiter.Core.ValueObjects;

namespace Arbiter.Core.Interfaces;

public interface IUpgrade
{
    Task<Stream> AcceptAsync(ReadOnlyHeaders? responseHeaders = null);
}
