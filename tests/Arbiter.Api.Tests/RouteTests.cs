using Arbiter.Api;
using Arbiter.Api.Results;
using Arbiter.Core.Enums;

namespace Arbiter.Api.Tests;

[TestFixture]
public class RouteTests
{
    [SetUp]
    public void Setup() => _routeTable = RouteTable.BuildFromTypes([typeof(TestController)]);

    private RouteTable? _routeTable;

    [Test]
    public void Match_ExactRoute_ReturnsParameters()
    {
        var result = _routeTable.Match(Method.Get, "/api/users");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.Route.Method, Is.EqualTo(Method.Get));
    }

    [Test]
    public void Match_RouteWithSingleParameter_ReturnsParameters()
    {
        var result = _routeTable.Match(Method.Get, "/api/users/123");

        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value.Parameters.ContainsKey("id"), Is.True);
            Assert.That(result.Value.Parameters["id"], Is.EqualTo("123"));
        }
    }

    [Test]
    public void Match_RouteWithMultipleParameters_ReturnsParameters()
    {
        var result = _routeTable.Match(Method.Get, "/api/users/123/items/456");

        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value.Parameters["id"], Is.EqualTo("123"));
            Assert.That(result.Value.Parameters["itemId"], Is.EqualTo("456"));
        }
    }

    [Test]
    public void Match_StaticSegment_ReturnsNullForWrongPath()
    {
        var result = _routeTable.Match(Method.Get, "/api/products");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Match_WrongMethod_ReturnsNull()
    {
        var result = _routeTable.Match(Method.Post, "/api/users/123");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Match_OptionalParameter_WithoutParameter_ReturnsNullParameter()
    {
        var result = _routeTable.Match(Method.Get, "/api/users/optional");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.Parameters.ContainsKey("id"), Is.False);
    }

    [Test]
    public void Match_OptionalParameter_WithParameter_ReturnsParameterValue()
    {
        var result = _routeTable.Match(Method.Get, "/api/users/optional/123");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.Parameters["id"], Is.EqualTo("123"));
    }

    [Test]
    public void Match_CatchAllParameter_ReturnsRemainingPath()
    {
        var result = _routeTable.Match(Method.Get, "/api/users/files/docs/2024/readme.txt");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.Parameters["path"], Is.EqualTo("docs/2024/readme.txt"));
    }

    [Test]
    public void Match_CatchAllParameter_SingleSegment_ReturnsSegment()
    {
        var result = _routeTable.Match(Method.Get, "/api/users/files/readme.txt");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.Parameters["path"], Is.EqualTo("readme.txt"));
    }

    [Test]
    public void Match_RouteWithIntConstraint_ValidInt_ReturnsParameters()
    {
        var result = _routeTable.Match(Method.Get, "/api/users/123");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.Parameters["id"], Is.EqualTo("123"));
    }

    [Test]
    public void Match_RouteWithIntConstraint_InvalidInt_ReturnsNull()
    {
        var result = _routeTable.Match(Method.Get, "/api/users/abc");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Match_AmbiguousRoutes_ThrowsException() => Assert.Throws<InvalidOperationException>(() => RouteTable.BuildFromTypes([typeof(AmbiguousController)]));
}