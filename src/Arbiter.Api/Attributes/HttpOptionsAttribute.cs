using Arbiter.Core.Enums;

namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HttpOptionsAttribute(string template = "") : HttpMethodAttribute(template, Method.Options)
{
}