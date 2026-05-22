using System.Diagnostics.CodeAnalysis;

namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class RouteAttribute([StringSyntax("Route")] string template) : Attribute
{
    public string Template
    {
        get;
    } = template;
}
