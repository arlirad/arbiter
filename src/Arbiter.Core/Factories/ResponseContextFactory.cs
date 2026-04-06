using Arbiter.Core.ValueObjects;

namespace Arbiter.Core.Factories;

public class ResponseContextFactory
{
    public static ResponseContext? Create()
    {
        return new ResponseContext();
    }
}