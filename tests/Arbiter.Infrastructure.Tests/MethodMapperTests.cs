using Arbiter.Core.Enums;
using Arbiter.Infrastructure.Mappers;

namespace Arbiter.Infrastructure.Tests;

public class MethodMapperTests
{
    [Test]
    public void ToEnum_returns_query_for_QUERY_token()
    {
        var result = MethodMapper.ToEnum("QUERY");

        Assert.That(result, Is.EqualTo(Method.Query));
    }

    [Test]
    public void ToString_returns_QUERY_for_query_method()
    {
        var result = MethodMapper.ToString(Method.Query);

        Assert.That(result, Is.EqualTo("QUERY"));
    }

    [Test]
    public void ToEnum_returns_null_for_unknown_token()
    {
        var result = MethodMapper.ToEnum("FROBNICATE");

        Assert.That(result, Is.Null);
    }

    [TestCase("GET", Method.Get)]
    [TestCase("HEAD", Method.Head)]
    [TestCase("POST", Method.Post)]
    [TestCase("PUT", Method.Put)]
    [TestCase("DELETE", Method.Delete)]
    [TestCase("PATCH", Method.Patch)]
    [TestCase("OPTIONS", Method.Options)]
    [TestCase("TRACE", Method.Trace)]
    [TestCase("QUERY", Method.Query)]
    public void ToEnum_maps_known_tokens(string token, Method expected)
    {
        var result = MethodMapper.ToEnum(token);

        Assert.That(result, Is.EqualTo(expected));
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
    public void ToString_maps_known_methods(Method method, string expected)
    {
        var result = MethodMapper.ToString(method);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("GET")]
    [TestCase("POST")]
    [TestCase("QUERY")]
    public void RoundTrip_preserves_token(string token)
    {
        var mapped = MethodMapper.ToEnum(token);

        Assert.That(mapped, Is.Not.Null);
        Assert.That(MethodMapper.ToString(mapped!.Value), Is.EqualTo(token));
    }
}
