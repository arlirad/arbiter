namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class FromFormAttribute : Attribute
{
    public string? Name
    {
        get;
        set;
    }
}
