namespace Arbiter.Api.Http;

public class SseEvent
{
    public string? Event
    {
        get;
        set;
    }
    public string? Data
    {
        get;
        set;
    }
    public string? Id
    {
        get;
        set;
    }
    public int? Retry
    {
        get;
        set;
    }

    public static SseEvent DataLine(string data) => new() {
        Data = data,
    };

    public static SseEvent EventLine(string eventName, string data) => new() {
        Event = eventName,
        Data = data,
    };
}