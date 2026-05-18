namespace Arbiter.Transport.Quic;

public class QuicPortService
{
    public List<int> Ports { get; set; } = [];
    public bool Announce
    {
        get; set;
    }
}