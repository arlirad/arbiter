using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Arbiter.Api;

namespace Arbiter.Api.Tests;

[TestFixture]
public class UnixSocketApiTests
{
    private IApi _api = null!;
    private HttpClient _client = null!;
    private CancellationTokenSource _cts = null!;
    private string _socketPath = null!;

    [SetUp]
    public async Task SetUp()
    {
        _socketPath = Path.Combine(Path.GetTempPath(), $"arbiter_api_test_{Guid.NewGuid():N}.sock");
        _cts = new CancellationTokenSource();

        var builder = ApiBuilder.Create(new Microsoft.Extensions.DependencyInjection.ServiceCollection())
            .WithUnixSocket(_socketPath);

        builder.ControllerTypes.Add(typeof(TestController));

        _api = builder.Build();

        _ = RunServer(_cts.Token);

        await Task.Yield();

        var handler = new SocketsHttpHandler {
            ConnectCallback = async (context, ct) => {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), ct);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };

        _client = new HttpClient(handler) {
            BaseAddress = new Uri("http://localhost"),
            Timeout = TimeSpan.FromSeconds(5),
        };
    }

    [TearDown]
    public async Task TearDown()
    {
        await _cts.CancelAsync();
        _client.Dispose();
        _cts.Dispose();

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

    private async Task RunServer(CancellationToken ct)
    {
        try
        {
            await _api.Run(ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Test]
    public async Task GET_all_returns_200()
    {
        var response = await _client.GetAsync("/api/users");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GET_by_id_returns_200()
    {
        var response = await _client.GetAsync("/api/users/42");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GET_by_id_invalid_int_returns_404()
    {
        var response = await _client.GetAsync("/api/users/abc");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task POST_create_returns_201_with_location()
    {
        var response = await _client.PostAsync("/api/users", null!);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location?.ToString(), Is.EqualTo("/api/users/1"));
    }

    [Test]
    public async Task DELETE_returns_204()
    {
        var response = await _client.DeleteAsync("/api/users/1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task PUT_returns_200()
    {
        var response = await _client.PutAsync("/api/users/1", null!);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Unknown_route_returns_404()
    {
        var response = await _client.GetAsync("/api/nonexistent");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Search_route_returns_200()
    {
        var response = await _client.GetAsync("/api/users/search");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public void Socket_file_created() => Assert.That(File.Exists(_socketPath), Is.True);

    [Test]
    public async Task Multiple_sequential_requests()
    {
        for (var i = 0; i < 5; i++)
        {
            var response = await _client.GetAsync("/api/users");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }

    [Test]
    public async Task Concurrent_requests()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(i => _client.GetAsync($"/api/users/{i}"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        Assert.That(responses.All(r => r.StatusCode == HttpStatusCode.OK), Is.True);
    }

    [Test]
    public async Task Nested_route_with_two_params()
    {
        var response = await _client.GetAsync("/api/users/1/items/2");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Catch_all_route()
    {
        var response = await _client.GetAsync("/api/users/files/docs/readme.txt");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Optional_param_without_value()
    {
        var response = await _client.GetAsync("/api/users/optional");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Optional_param_with_value()
    {
        var response = await _client.GetAsync("/api/users/optional/99");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
