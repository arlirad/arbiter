namespace Arbiter.Application.Configuration;

public class TransportConfig
{
    public int Backlog { get; set; } = 128;
    public int QueueSize { get; set; } = 4096;
}