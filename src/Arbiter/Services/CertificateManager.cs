using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Arbiter.Services;

internal class CertificateManager
{
    private readonly ConcurrentDictionary<string, X509Certificate2> _certificates = [];
    private readonly X509Certificate2 _fallbackCertificate = CreateFallback();

    public void Set(string hostName, X509Certificate2 certificate)
    {
        _certificates[hostName] = certificate;
    }

    public X509Certificate2? Get(string hostName)
    {
        return _certificates.GetValueOrDefault(hostName);
    }

    public X509Certificate2 GetFallback()
    {
        return _fallbackCertificate;
    }

    private static X509Certificate2 CreateFallback()
    {
        var ecdsa = ECDsa.Create();
        var req = new CertificateRequest("cn=foobar", ecdsa, HashAlgorithmName.SHA256);

        return req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
    }
}