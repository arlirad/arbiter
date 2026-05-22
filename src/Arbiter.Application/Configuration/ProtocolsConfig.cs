using Arbiter.Core.Enums;

namespace Arbiter.Application.Configuration;

public class ProtocolsConfig
{
    public bool Http11
    {
        get;
        set;
    } = true;
    public bool Http2
    {
        get;
        set;
    }
    public bool Http3
    {
        get;
        set;
    } = true;

    public HashSet<Protocol> ToSet()
    {
        var set = new HashSet<Protocol>();

        if (Http11)
            set.Add(Protocol.Http11);

        if (Http2)
            set.Add(Protocol.Http2);

        if (Http3)
            set.Add(Protocol.Http3);

        return set;
    }
}
