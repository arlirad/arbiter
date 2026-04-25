using Arbiter.Core.Enums;

namespace Arbiter.Api.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class HttpHeadAttribute(string template = "") : HttpMethodAttribute(template, Method.Head)
{
}