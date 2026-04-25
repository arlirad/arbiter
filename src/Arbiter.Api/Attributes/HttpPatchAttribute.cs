using Arbiter.Core.Enums;

namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HttpPatchAttribute() : HttpMethodAttribute(Method.Patch)
{
    public HttpPatchAttribute(string template) : this()
    {
        Template = template;
    }
}