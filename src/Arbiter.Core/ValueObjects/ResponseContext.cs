using Arbiter.Core.Enums;

namespace Arbiter.Core.ValueObjects;

public class ResponseContext
{
    internal ResponseContext()
    {
        Headers = [];
        Headers.Add("Server", "Arbiter");
    }

    public Status? Status
    {
        get;
        private set;
    }
    public Headers Headers
    {
        get;
    }
    public Stream? Stream
    {
        get;
        private set;
    }

    public ValueTask Set(Status status, Stream? stream = null)
    {
        Status = status;
        Stream = stream;

        return ValueTask.CompletedTask;
    }
}