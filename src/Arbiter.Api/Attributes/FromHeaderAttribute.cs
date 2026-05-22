namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class FromHeaderAttribute : Attribute
{
    public FromHeaderAttribute()
    {
    }

    public FromHeaderAttribute(string name)
    {
        Name = name;
    }

    public string? Name
    {
        get;
    }
}
