using Arbiter.Core.Enums;
using Arbiter.Core.Factories;

namespace Arbiter.Core.Tests;

public class ResponseContextTests
{
    [Test]
    public void AddHeader_appends_value_to_existing_header()
    {
        var response = ResponseContextFactory.Create()!;

        response.AddHeader("X", "a");
        response.AddHeader("X", "b");

        Assert.That(response.Headers["X"], Is.EqualTo(["a", "b"]));
    }

    [Test]
    public void SetHeader_replaces_all_values_with_single_value()
    {
        var response = ResponseContextFactory.Create()!;

        response.AddHeader("X", "a");
        response.AddHeader("X", "b");
        response.SetHeader("X", "c");

        Assert.That(response.Headers["X"], Is.EqualTo(["c"]));
    }

    [Test]
    public void SetHeader_list_overload_replaces_all_values()
    {
        var response = ResponseContextFactory.Create()!;

        response.AddHeader("X", "a");
        response.AddHeader("X", "b");
        response.SetHeader("X", ["c", "d"]);

        Assert.That(response.Headers["X"], Is.EqualTo(["c", "d"]));
    }

    [Test]
    public void SetHeader_list_overload_stores_reference_without_copy()
    {
        var response = ResponseContextFactory.Create()!;
        var values = new List<string> { "c", "d" };

        response.SetHeader("X", values);

        Assert.That(response.Headers["X"], Is.SameAs(values));
    }

    [Test]
    public void SetHeader_list_overload_copies_non_list_input()
    {
        var response = ResponseContextFactory.Create()!;
        var values = new[] { "a", "b" };

        response.SetHeader("X", values);
        values[0] = "mutated";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Headers["X"], Is.EqualTo(["a", "b"]));
            Assert.That(response.Headers["X"], Is.Not.SameAs(values));
        }
    }

    [Test]
    public void RemoveHeader_returns_true_and_removes()
    {
        var response = ResponseContextFactory.Create()!;

        response.AddHeader("X", "a");

        var removed = response.RemoveHeader("X");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.True);
            Assert.That(response.Headers["X"], Is.Null);
        }
    }

    [Test]
    public void RemoveHeader_returns_false_when_absent()
    {
        var response = ResponseContextFactory.Create()!;

        var removed = response.RemoveHeader("X-Missing");

        Assert.That(removed, Is.False);
    }

    [Test]
    public void AppendHeader_appends_value_when_header_absent()
    {
        var response = ResponseContextFactory.Create()!;

        response.AppendHeader("Vary", "Origin");

        Assert.That(response.Headers["Vary"], Is.EqualTo(["Origin"]));
    }

    [Test]
    public void AppendHeader_skips_existing_case_insensitive_value()
    {
        var response = ResponseContextFactory.Create()!;

        response.AddHeader("Vary", "origin");
        response.AppendHeader("Vary", "Origin");

        Assert.That(response.Headers["Vary"], Is.EqualTo(["origin"]));

        response.AppendHeader("Vary", "Accept-Encoding");

        Assert.That(response.Headers["Vary"], Is.EqualTo(["origin", "Accept-Encoding"]));
    }

    [Test]
    public void ContentType_round_trips()
    {
        var response = ResponseContextFactory.Create()!;

        response.ContentType = "text/html";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.ContentType, Is.EqualTo("text/html"));
            Assert.That(response.Headers["Content-Type"], Is.EqualTo(["text/html"]));
        }
    }

    [Test]
    public void ContentType_null_removes_header()
    {
        var response = ResponseContextFactory.Create()!;

        response.ContentType = "text/html";
        response.ContentType = null;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.ContentType, Is.Null);
            Assert.That(response.Headers["Content-Type"], Is.Null);
        }
    }

    [Test]
    public void Header_returns_first_value_or_null()
    {
        var response = ResponseContextFactory.Create()!;

        response.AddHeader("Accept", "text/plain");
        response.AddHeader("Accept", "text/html");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Header("Accept"), Is.EqualTo("text/plain"));
            Assert.That(response.Header("X-Missing"), Is.Null);
        }
    }

    [Test]
    public async Task HasResponse_false_until_set()
    {
        var response = ResponseContextFactory.Create()!;

        Assert.That(response.HasResponse, Is.False);

        await response.Set(Status.Ok);

        Assert.That(response.HasResponse, Is.True);
    }

    [Test]
    public void Headers_view_reflects_mutations()
    {
        var response = ResponseContextFactory.Create()!;
        var view = response.Headers;

        response.AddHeader("X", "a");
        response.SetHeader("Y", "b");
        response.RemoveHeader("Y");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view["X"], Is.EqualTo(["a"]));
            Assert.That(view["Y"], Is.Null);
        }

        response.AddHeader("X", "b");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view["X"], Is.EqualTo(["a", "b"]));
            Assert.That(view["Y"], Is.Null);
        }
    }

    [Test]
    public async Task Set_stores_status_and_stream()
    {
        var response = ResponseContextFactory.Create()!;
        using var stream = new MemoryStream();

        await response.Set(Status.Ok, stream);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Status, Is.EqualTo(Status.Ok));
            Assert.That(response.Stream, Is.SameAs(stream));
        }
    }
}
