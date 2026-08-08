using Arbiter.Core.Enums;

namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HttpQueryAttribute() : HttpMethodAttribute(Method.Query)
{
    public HttpQueryAttribute(string template) : this()
    {
        Template = template;
    }
}
