using Arbiter.Core.Enums;

namespace Arbiter.Application.Interfaces;

public interface IProtocolRegistration
{
    Protocol Protocol
    {
        get;
    }
    IProtocol Create(IServiceProvider sp);
}
