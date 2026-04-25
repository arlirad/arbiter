namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class FromQueryAttribute : Attribute
{
    public FromQueryAttribute()
    {
    }

    public FromQueryAttribute(string name)
    {
        Name = name;
    }

    public string? Name
    {
        get;
    }
}