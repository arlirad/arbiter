using Arbiter.Core.Enums;

namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HttpPostAttribute() : HttpMethodAttribute(Method.Post)
{
    public HttpPostAttribute(string template) : this()
    {
        Template = template;
    }
}
