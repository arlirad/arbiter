using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Arbiter.Http3.Tests.Helpers;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public static class SelfSignedCertificate
{
    public static X509Certificate2 Create(string commonName = "localhost")
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest(
            $"cn={commonName}",
            ecdsa,
            HashAlgorithmName.SHA256
        );

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(commonName);
        req.CertificateExtensions.Add(sanBuilder.Build());

        return req.CreateSelfSigned(
            DateTimeOffset.Now,
            DateTimeOffset.Now.AddYears(5)
        );
    }
}
