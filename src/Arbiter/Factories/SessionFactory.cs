using System.Net.Sockets;
using Arbiter.Network;

internal class SessionFactory
{
    public Session Create(Socket socket)
    {
        return new Session(socket);
    }
}