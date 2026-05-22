namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class FromRouteAttribute : Attribute
{
    public FromRouteAttribute()
    {
    }

    public FromRouteAttribute(string name)
    {
        Name = name;
    }

    public string? Name
    {
        get;
    }
}
