using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Arbiter.Api;
using Arbiter.Api.Attributes;
using Arbiter.Api.Controllers;
using Arbiter.Api.Http;
using Arbiter.Api.Results;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Proxy;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Infrastructure.Proxy.Tests;

[TestFixture]
public class ProxyToUnixSocketTests
{
    private static readonly ContextFactory _contextFactory = new();

    private IApi _api = null!;
    private CancellationTokenSource _apiCts = null!;
    private string _socketPath = null!;
    private ProxyMiddleware _proxy = null!;
    private Site _site = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    }

    [SetUp]
    public async Task SetUp()
    {
        _socketPath = Path.Combine(Path.GetTempPath(), $"arbiter_proxy_test_{Guid.NewGuid():N}.sock");
        _apiCts = new CancellationTokenSource();

        var builder = ApiBuilder.Create(new Microsoft.Extensions.DependencyInjection.ServiceCollection())
            .WithUnixSocket(_socketPath);

        builder.ControllerTypes.Add(typeof(ProxyTestController));

        _api = builder.Build();

        Exception? apiException = null;

        _ = Task.Run(async () => {
            try
            {
                await _api.Run(_apiCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                apiException = ex;
            }
        });

        await Task.Yield();

        for (var i = 0; i < 50; i++)
        {
            if (File.Exists(_socketPath) || apiException is not null)
                break;
            await Task.Delay(100);
        }

        if (apiException is not null)
            Assert.Fail($"API server failed to start: {apiException}");

        Assert.That(File.Exists(_socketPath), Is.True);

        _proxy = new ProxyMiddleware();

        var configDict = new Dictionary<string, string?> { { "Target", $"unix://{_socketPath}" } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

        _site = new Site("proxy-test", [], [], [], _ => Task.CompletedTask);
        await _proxy.Configure(_site, config);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _apiCts.CancelAsync();
        _apiCts.Dispose();

        if (File.Exists(_socketPath))
        {
            try
            {
                File.Delete(_socketPath);
            }
            catch
            {
            }
        }
    }

    private static Context CreateContext(
        Method method = Method.Get,
        string path = "/",
        Stream? body = null,
        string? authority = "localhost",
        Dictionary<string, List<string>>? headers = null)
    {
        var requestHeaders = headers ?? new Dictionary<string, List<string>> {
            { "Host", [authority ?? "localhost"] },
        };

        return _contextFactory.Create(
            1,
            method,
            path,
            requestHeaders,
            body,
            null,
            authority,
            false,
            null
        )!;
    }

    [Test]
    public async Task GET_proxies_to_unix_socket_api()
    {
        var context = CreateContext(Method.Get, "/api/hello");

        await _proxy.Handle(context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.Ok));
    }

    [Test]
    public async Task GET_proxies_body_back()
    {
        var context = CreateContext(Method.Get, "/api/hello");

        await _proxy.Handle(context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.Ok));
        Assert.That(context.Response.Stream, Is.Not.Null);

        using var reader = new StreamReader(context.Response.Stream!);
        var body = await reader.ReadToEndAsync();
        Assert.That(body, Is.EqualTo("\"Hello from Unix socket!\""));
    }

    [Test]
    public async Task POST_proxies_request_body()
    {
        var requestBody = "posted data";
        var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));

        var headers = new Dictionary<string, List<string>> {
            { "Host", ["localhost"] },
            { "Content-Type", ["text/plain"] },
        };

        var context = CreateContext(Method.Post, "/api/echo", bodyStream, "localhost", headers);

        await _proxy.Handle(context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.Ok));
        Assert.That(context.Response.Stream, Is.Not.Null);

        using var reader = new StreamReader(context.Response.Stream!);
        var body = await reader.ReadToEndAsync();
        Assert.That(body, Is.EqualTo("\"posted data\""));
    }

    [Test]
    public async Task Proxies_404_for_unknown_route()
    {
        var context = CreateContext(Method.Get, "/nonexistent");

        await _proxy.Handle(context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.NotFound));
    }

    [Test]
    public async Task DELETE_proxies_correctly()
    {
        var context = CreateContext(Method.Delete, "/api/hello");

        await _proxy.Handle(context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.Ok));
    }

    [Test]
    public async Task PUT_proxies_correctly()
    {
        var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes("updated"));
        var headers = new Dictionary<string, List<string>> {
            { "Host", ["localhost"] },
            { "Content-Type", ["text/plain"] },
        };

        var context = CreateContext(Method.Put, "/api/echo", bodyStream, "localhost", headers);

        await _proxy.Handle(context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.Ok));
    }

    [Test]
    public async Task Response_content_type_forwarded()
    {
        var context = CreateContext(Method.Get, "/api/hello");

        await _proxy.Handle(context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.Ok));
        Assert.That(context.Response.Headers.ContentType, Does.Contain("application/json"));
    }

    [Test]
    public async Task Multiple_sequential_requests()
    {
        for (var i = 0; i < 5; i++)
        {
            var context = CreateContext(Method.Get, "/api/hello");
            await _proxy.Handle(context);
            Assert.That(context.Response.Status, Is.EqualTo(Status.Ok));
        }
    }

    [Test]
    public async Task Concurrent_requests()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(async () => {
                var context = CreateContext(Method.Get, "/api/hello");
                await _proxy.Handle(context);
                return context;
            }))
            .ToList();

        var contexts = await Task.WhenAll(tasks);

        Assert.That(contexts.All(c => c.Response.Status == Status.Ok), Is.True);
    }
}

[Route("api")]
public class ProxyTestController : IApiController
{
    [HttpGet("hello")]
    public IActionResult Hello() => new OkObjectResult("Hello from Unix socket!");

    [HttpPost("echo")]
    public async Task<IActionResult> EchoPost(HttpContext ctx)
    {
        using var ms = new MemoryStream();
        if (ctx.Request.Body is not null)
            await ctx.Request.Body.CopyToAsync(ms);

        var body = Encoding.UTF8.GetString(ms.ToArray());
        return new OkObjectResult(body);
    }

    [HttpPut("echo")]
    public async Task<IActionResult> EchoPut(HttpContext ctx)
    {
        using var ms = new MemoryStream();
        if (ctx.Request.Body is not null)
            await ctx.Request.Body.CopyToAsync(ms);

        var body = Encoding.UTF8.GetString(ms.ToArray());
        return new OkObjectResult(body);
    }

    [HttpGet("headers")]
    public IActionResult Headers() => new OkResult();

    [HttpDelete("hello")]
    public IActionResult DeleteHello() => new OkResult();
}
