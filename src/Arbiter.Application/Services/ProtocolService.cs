using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;

namespace Arbiter.Application.Services;

public sealed class ProtocolService(IEnumerable<IProtocolRegistration> registrations, IServiceProvider sp) : IProtocolFactory
{
    private readonly Dictionary<Protocol, Func<IServiceProvider, IProtocol>> _factories = registrations.ToDictionary(r => r.Protocol, r => new Func<IServiceProvider, IProtocol>(r.Create));

    public IProtocol Create(Protocol protocol)
    {
        return _factories.TryGetValue(protocol, out var factory)
            ? factory(sp)
            : throw new NotSupportedException($"Protocol {protocol} is not supported");
    }
}
