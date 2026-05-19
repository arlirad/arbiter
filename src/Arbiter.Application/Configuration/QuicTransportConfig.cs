namespace Arbiter.Application.Configuration;

public class QuicTransportConfig : IpTransportConfig
{
    public int MaxInboundBiStreams { get; set; } = 1024;
    public AnnounceConfig? Announce
    {
        get; init;
    }
}