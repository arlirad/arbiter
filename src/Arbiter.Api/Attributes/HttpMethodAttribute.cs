using System.Diagnostics.CodeAnalysis;
using Arbiter.Core.Enums;

namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public abstract class HttpMethodAttribute(Method method) : Attribute
{
    protected HttpMethodAttribute([StringSyntax("Route")] string template, Method method) : this(method)
    {
        Template = template;
    }

    public string Template
    {
        get;
        set;
    } = "";
    public Method Method
    {
        get;
    } = method;
}
