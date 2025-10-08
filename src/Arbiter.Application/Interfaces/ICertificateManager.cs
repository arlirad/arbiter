using System.Security.Cryptography.X509Certificates;

namespace Arbiter.Application.Interfaces;

public interface ICertificateManager
{
    void Set(string hostName, X509Certificate2 certificate);
    X509Certificate2? Get(string hostName);
    X509Certificate2 GetFallback();
}