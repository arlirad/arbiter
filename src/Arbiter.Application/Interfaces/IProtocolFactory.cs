using Arbiter.Core.Enums;

namespace Arbiter.Application.Interfaces;

public interface IProtocolFactory
{
    IProtocol Create(Protocol protocol);
}
