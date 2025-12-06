using Arbiter.Domain.ValueObjects;

namespace Arbiter.Domain.Factories;

public class ResponseContextFactory
{
    public static ResponseContext? Create()
    {
        return new ResponseContext();
    }
}