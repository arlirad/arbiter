using System.Net.Sockets;

namespace Arbiter.Services;

internal interface IAcceptor
{
    Task<Socket> Accept();
}