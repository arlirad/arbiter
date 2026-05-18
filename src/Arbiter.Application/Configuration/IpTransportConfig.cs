using System.Net;

namespace Arbiter.Application.Configuration;

public class IpTransportConfig : TransportConfig
{
    public List<int> Ports { get; set; } = [];
}