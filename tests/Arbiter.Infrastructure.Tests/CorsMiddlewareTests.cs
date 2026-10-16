using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Cors;
using Arbiter.Infrastructure.Cors.Config;

namespace Arbiter.Infrastructure.Tests;

public class CorsMiddlewareTests
{
    [Test]
    public async Task Wildcard_origin_matches_any_origin()
    {
        var middleware = CreateMiddleware(["*"]);

        var context = CreateContext(Method.Get, new Dictionary<string, List<string>> {
            ["Origin"] = ["https://example.com"],
        });

        await middleware.Handle(context);

        Assert.That(context.Response.Headers["Access-Control-Allow-Origin"]?.FirstOrDefault(),
            Is.EqualTo("https://example.com"));
    }

    [Test]
    public async Task Exact_origin_matches()
    {
        var middleware = CreateMiddleware(["https://example.com"]);

        var context = CreateContext(Method.Get, new Dictionary<string, List<string>> {
            ["Origin"] = ["https://example.com"],
        });

        await middleware.Handle(context);

        Assert.That(context.Response.Headers["Access-Control-Allow-Origin"]?.FirstOrDefault(),
            Is.EqualTo("https://example.com"));
    }

    [Test]
    public async Task Non_matching_origin_does_not_set_header()
    {
        var middleware = CreateMiddleware(["https://allowed.com"]);

        var context = CreateContext(Method.Get, new Dictionary<string, List<string>> {
            ["Origin"] = ["https://evil.com"],
        });

        await middleware.Handle(context);

        Assert.That(context.Response.Headers["Access-Control-Allow-Origin"], Is.Null);
    }

    [Test]
    public async Task No_origin_header_does_not_set_allow_origin()
    {
        var middleware = CreateMiddleware(["*"]);

        var context = CreateContext(Method.Get, []);

        await middleware.Handle(context);

        Assert.That(context.Response.Headers["Access-Control-Allow-Origin"], Is.Null);
    }

    [Test]
    public async Task Options_sets_allow_methods_and_headers()
    {
        var middleware = CreateMiddleware(["*"], ["GET", "POST"], ["Content-Type"]);

        var context = CreateContext(Method.Options, new Dictionary<string, List<string>> {
            ["Origin"] = ["https://example.com"],
        });

        await middleware.Handle(context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Response.Status, Is.EqualTo(Status.Ok));
            Assert.That(context.Response.Headers["Access-Control-Allow-Methods"]?.FirstOrDefault(),
                Is.EqualTo("GET, POST"));

            Assert.That(context.Response.Headers["Access-Control-Allow-Headers"]?.FirstOrDefault(),
                Is.EqualTo("Content-Type"));
        }
    }

    [Test]
    public async Task Options_preserves_query_method_in_allow_methods()
    {
        var middleware = CreateMiddleware(["*"], ["GET", "QUERY"], ["Content-Type"]);

        var context = CreateContext(Method.Options, new Dictionary<string, List<string>> {
            ["Origin"] = ["https://example.com"],
        });

        await middleware.Handle(context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Response.Status, Is.EqualTo(Status.Ok));
            Assert.That(context.Response.Headers["Access-Control-Allow-Methods"]?.FirstOrDefault(),
                Is.EqualTo("GET, QUERY"));
        }
    }

    [Test]
    public async Task Matching_origin_appends_vary_origin()
    {
        var middleware = new CorsMiddleware(ctx => {
            ctx.Response.AddHeader("Vary", "Accept-Encoding");
            return Task.CompletedTask;
        });

        await middleware.Configure(null!, new CorsConfig { AllowOrigin = ["*"] });

        var context = CreateContext(Method.Get, new Dictionary<string, List<string>> {
            ["Origin"] = ["https://example.com"],
        });

        await middleware.Handle(context);

        Assert.That(context.Response.Headers["Vary"], Is.EqualTo(new List<string> { "Accept-Encoding", "Origin" }));
    }

    [Test]
    public async Task Matching_origin_sets_vary_origin_when_request_has_no_vary()
    {
        var middleware = CreateMiddleware(["*"]);

        var context = CreateContext(Method.Get, new Dictionary<string, List<string>> {
            ["Origin"] = ["https://example.com"],
        });

        await middleware.Handle(context);

        Assert.That(context.Response.Headers["Vary"], Is.EqualTo(new List<string> { "Origin" }));
    }

    private static CorsMiddleware CreateMiddleware(
        List<string> origins,
        List<string>? methods = null,
        List<string>? headers = null)
    {
        var middleware = new CorsMiddleware(_ => Task.CompletedTask);

        var config = new CorsConfig {
            AllowOrigin = origins,
            AllowMethods = methods,
            AllowHeaders = headers,
        };

        middleware.Configure(null!, config);

        return middleware;
    }

    private static Context CreateContext(Method method, Dictionary<string, List<string>> headers)
    {
        var requestHeaders = new Headers();
        foreach (var (key, values) in headers)
            requestHeaders[key] = values;

        var request = RequestContextFactory.Create(1, method, "/", requestHeaders, null, null, null, false, null)!;
        var response = ResponseContextFactory.Create()!;

        return new Context(request, response);
    }
}
