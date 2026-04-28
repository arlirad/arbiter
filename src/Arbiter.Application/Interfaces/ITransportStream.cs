namespace Arbiter.Application.Interfaces;

public interface ITransportStream
{
    Stream Stream
    {
        get;
    }
    long StreamId
    {
        get;
    }
}
