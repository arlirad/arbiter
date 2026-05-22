using Arbiter.Configuration;

namespace Arbiter.Transport.Configuration;

public class TransportConfig : ITransportConfig
{
    public int Backlog
    {
        get;
        set;
    } = 128;
    public int QueueSize
    {
        get;
        set;
    } = 4096;
}
