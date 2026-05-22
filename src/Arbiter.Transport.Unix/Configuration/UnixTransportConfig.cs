using Arbiter.Transport.Configuration;

namespace Arbiter.Transport.Unix.Configuration;

public class UnixTransportConfig : TransportConfig
{
    public List<string> Paths
    {
        get;
        set;
    } = [];
}
