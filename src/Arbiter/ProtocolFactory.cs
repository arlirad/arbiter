using System.Runtime.Versioning;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Middleware;
using Arbiter.Protocol.Http11;
using Arlirad.Http3;

public sealed class ProtocolFactory(TransactionIdProvider transactionIdProvider) : IProtocolFactory
{
    public IProtocol Create(global::Arbiter.Core.Enums.Protocol protocol) => protocol switch {
        global::Arbiter.Core.Enums.Protocol.Http11 => new Http11Protocol(transactionIdProvider),
        global::Arbiter.Core.Enums.Protocol.Http3 => CreateHttp3Protocol(),
        _ => throw new NotSupportedException($"Protocol {protocol} is not supported"),
    };

    private IProtocol CreateHttp3Protocol()
    {
        return IsHttp3Supported()
            ? (IProtocol)new Http3Protocol(transactionIdProvider)
            : throw new PlatformNotSupportedException("HTTP/3 is only supported on Linux, macOS, and Windows.");
    }

    [SupportedOSPlatformGuard("linux")]
    [SupportedOSPlatformGuard("macOS")]
    [SupportedOSPlatformGuard("windows")]
    private static bool IsHttp3Supported() => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();
}
