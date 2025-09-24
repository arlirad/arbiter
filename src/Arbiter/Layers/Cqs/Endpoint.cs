using System.Reflection.Metadata;
using Arbiter.Handlers.Cqs;

public class Endpoint
{
    protected static Result Ok()
    {
        return new Result(200);
    }

    protected static Result Ok(object result)
    {
        return new Result(200, result);
    }
}