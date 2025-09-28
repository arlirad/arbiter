using System.Net.Sockets;
using Arbiter.Infrastructure.Network;
using Arbiter.Services;

internal class SessionFactory(CertificateManager certificateManager)
{
    public Session Create(Socket socket)
    {
        return new Session(socket, certificateManager);
    }
}