namespace Arbiter.Transport.Configuration;

public class IpTransportConfig : TransportConfig
{
    public List<int> Ports
    {
        get;
        set;
    } = [];
}
