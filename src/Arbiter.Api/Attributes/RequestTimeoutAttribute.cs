namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
public class RequestTimeoutAttribute(int seconds = 30) : Attribute
{
    public int Seconds
    {
        get;
    } = seconds;
}
