using Arbiter.Application.Services;

namespace Arbiter.Application.Tests;

public class AltSvcServiceTests
{
    [Test]
    public void HeaderValue_is_null_initially()
    {
        var sut = new AltSvcService();

        Assert.That(sut.HeaderValue, Is.Null);
    }

    [Test]
    public void Set_builds_header_value()
    {
        var sut = new AltSvcService();

        sut.Set("h3", ":443", 86400);

        Assert.That(sut.HeaderValue, Is.EqualTo(@"h3="":443""; ma=86400"));
    }

    [Test]
    public void Set_with_persist_appends_persist_flag()
    {
        var sut = new AltSvcService();

        sut.Set("h3", ":443", 86400, true);

        Assert.That(sut.HeaderValue, Is.EqualTo(@"h3="":443""; ma=86400; persist=1"));
    }

    [Test]
    public void Set_overwrites_existing_entry()
    {
        var sut = new AltSvcService();

        sut.Set("h3", ":443", 86400);
        sut.Set("h3", ":8443", 3600);

        Assert.That(sut.HeaderValue, Is.EqualTo(@"h3="":8443""; ma=3600"));
    }

    [Test]
    public void Remove_clears_entry()
    {
        var sut = new AltSvcService();

        sut.Set("h3", ":443", 86400);
        sut.Remove("h3");

        Assert.That(sut.HeaderValue, Is.Null);
    }

    [Test]
    public void Remove_nonexistent_is_noop()
    {
        var sut = new AltSvcService();

        sut.Remove("h3");

        Assert.That(sut.HeaderValue, Is.Null);
    }

    [Test]
    public void Multiple_entries_are_comma_separated()
    {
        var sut = new AltSvcService();

        sut.Set("h3", ":443", 86400);
        sut.Set("h2", ":443", 3600);

        Assert.That(sut.HeaderValue, Is.EqualTo(@"h3="":443""; ma=86400, h2="":443""; ma=3600"));
    }

    [Test]
    public void Remove_one_of_multiple_preserves_other()
    {
        var sut = new AltSvcService();

        sut.Set("h3", ":443", 86400);
        sut.Set("h2", ":443", 3600);
        sut.Remove("h3");

        Assert.That(sut.HeaderValue, Is.EqualTo(@"h2="":443""; ma=3600"));
    }
}
