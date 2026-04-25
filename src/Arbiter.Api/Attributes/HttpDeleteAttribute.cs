using Arbiter.Core.Enums;

namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HttpDeleteAttribute() : HttpMethodAttribute(Method.Delete)
{
    public HttpDeleteAttribute(string template) : this()
    {
        Template = template;
    }
}