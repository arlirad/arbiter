using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;

namespace Arbiter.Application.Services;

public sealed class ProtocolRegistration(Protocol protocol, Func<IServiceProvider, IProtocol> factory) : IProtocolRegistration
{
    public Protocol Protocol
    {
        get;
    } = protocol;
    public IProtocol Create(IServiceProvider sp) => factory(sp);
}
