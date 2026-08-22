using Arbiter.Core.Enums;
using Arbiter.Core.Factories;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Core.Tests;

public class RequestContextTests
{
    [Test]
    public void Header_returns_first_value()
    {
        var request = CreateRequest();

        Assert.That(request.Header("Accept"), Is.EqualTo("text/plain"));
    }

    [Test]
    public void Header_returns_null_when_missing()
    {
        var request = CreateRequest();

        Assert.That(request.Header("X-Missing"), Is.Null);
    }

    [Test]
    public void RewritePath_replaces_path()
    {
        var request = CreateRequest();

        request.RewritePath("/new");

        Assert.That(request.Path, Is.EqualTo("/new"));
    }

    [Test]
    public void Path_is_readable()
    {
        var request = CreateRequest();

        Assert.That(request.Path, Is.EqualTo("/old"));
    }

    private static RequestContext CreateRequest()
    {
        var headers = new Headers();
        headers["Accept"] = ["text/plain", "text/html"];

        return RequestContextFactory.Create(1, Method.Get, "/old", headers, null, null, null, false, null)!;
    }
}
