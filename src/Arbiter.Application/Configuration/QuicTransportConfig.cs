namespace Arbiter.Application.Configuration;

public class QuicTransportConfig : IpTransportConfig
{
    public bool Announce
    {
        get; set;
    }
    public int MaxInboundBiStreams { get; set; } = 1024;
}