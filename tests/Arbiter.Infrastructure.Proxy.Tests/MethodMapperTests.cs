using Arbiter.Core.Enums;
using Arbiter.Infrastructure.Proxy.Mappers;

namespace Arbiter.Infrastructure.Proxy.Tests;

public class MethodMapperTests
{
    [Test]
    public void ToHttpMethod_maps_query_to_HttpMethod_Query()
    {
        var httpMethod = MethodMapper.ToHttpMethod(Method.Query);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(httpMethod.Method, Is.EqualTo("QUERY"));
            Assert.That(httpMethod, Is.EqualTo(HttpMethod.Query));
        }
    }

    [TestCase(Method.Get, "GET")]
    [TestCase(Method.Head, "HEAD")]
    [TestCase(Method.Post, "POST")]
    [TestCase(Method.Put, "PUT")]
    [TestCase(Method.Delete, "DELETE")]
    [TestCase(Method.Patch, "PATCH")]
    [TestCase(Method.Options, "OPTIONS")]
    [TestCase(Method.Trace, "TRACE")]
    [TestCase(Method.Query, "QUERY")]
    public void ToHttpMethod_maps_known_methods(Method method, string expected)
    {
        var httpMethod = MethodMapper.ToHttpMethod(method);

        Assert.That(httpMethod.Method, Is.EqualTo(expected));
    }

    [Test]
    public void ToHttpMethod_maps_get_and_post_to_canonical_HttpMethod()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MethodMapper.ToHttpMethod(Method.Get), Is.EqualTo(HttpMethod.Get));
            Assert.That(MethodMapper.ToHttpMethod(Method.Post), Is.EqualTo(HttpMethod.Post));
        }
    }
}
