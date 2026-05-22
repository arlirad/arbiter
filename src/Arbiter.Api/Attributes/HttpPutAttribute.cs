using Arbiter.Core.Enums;

namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HttpPutAttribute() : HttpMethodAttribute(Method.Put)
{
    public HttpPutAttribute(string template) : this()
    {
        Template = template;
    }
}
