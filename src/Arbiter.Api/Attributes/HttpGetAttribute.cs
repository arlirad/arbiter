using Arbiter.Core.Enums;

namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HttpGetAttribute() : HttpMethodAttribute(Method.Get)
{
    public HttpGetAttribute(string template) : this()
    {
        Template = template;
    }
}