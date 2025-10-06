using System.Net.Sockets;

namespace Arbiter.Transport.Abstractions;

public interface IAcceptor
{
    Task<Socket> Accept();
}