using Arbiter.Domain.Enums;

namespace Arbiter.Domain.ValueObjects;

public class ResponseContext
{
    internal ResponseContext()
    {
        Headers = new Headers();
    }

    public Status? Status { get; private set; }
    public Headers Headers { get; }
    public Stream? Stream { get; private set; }

    public ValueTask Set(Status status, Stream? stream = null)
    {
        Status = status;
        Stream = stream;

        return ValueTask.CompletedTask;
    }
}