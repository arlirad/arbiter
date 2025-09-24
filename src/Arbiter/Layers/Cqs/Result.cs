namespace Arbiter.Handlers.Cqs;

public class Result
{
    public int Code { get; }

    public object? Body { get; }

    public Result(int code)
    {
        Code = code;
    }

    public Result(int code, object body)
    {
        Code = code;
        Body = body;
    }
}