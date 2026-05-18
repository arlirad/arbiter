namespace Arbiter.Application.Configuration;

public class UnixTransportConfig : TransportConfig
{
    public List<string> Paths { get; set; } = [];
}