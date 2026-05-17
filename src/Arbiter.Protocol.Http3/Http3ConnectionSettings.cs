namespace Arlirad.Http3;

public class Http3ConnectionSettings
{
    public ulong MaxFieldSectionSize
    {
        get;
        set;
    }
    public int MaxDecoderDynamicTableCapacity
    {
        get;
        set;
    }
    public bool EnableConnectProtocol
    {
        get;
        set;
    }
    public int MaxEncoderDynamicTableCapacity
    {
        get;
        set;
    } = 8192;
    public int QPackBlockedStreams
    {
        get;
        set;
    } = 0;
}
