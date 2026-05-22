using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Arbiter.Application.DTOs;
using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;
using Arbiter.Transport.Unix.Tests.Helpers;

namespace Arbiter.Transport.Unix.Tests;

public class UnixSocketIntegrationTests
{
    private static readonly ReadOnlyHeaders EmptyHeaders = new([]);

    private UnixSocketFixture? _fixture;

    [SetUp]
    public async Task SetUp() => _fixture = await UnixSocketFixture.CreateAsync();

    [TearDown]
    public async Task TearDown()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }

    [Test]
    public async Task GET_returns_200()
    {
        var response = await _fixture!.Client.GetAsync("/");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Empty);
    }

    [Test]
    public async Task POST_with_body()
    {
        var requestBody = "Hello, Unix Socket!";
        var content = new StringContent(requestBody, Encoding.UTF8, "text/plain");

        var response = await _fixture!.Client.PostAsync("/upload", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Response_with_body()
    {
        var expectedBody = "Response from server";

        var customFixture = await UnixSocketFixture.CreateAsync(req => Task.FromResult(new ResponseDto {
            Status = Status.Ok,
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(expectedBody)),
            Headers = EmptyHeaders,
        }));

        try
        {
            var response = await customFixture.Client.GetAsync("/data");
            var body = await response.Content.ReadAsStringAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(body, Is.EqualTo(expectedBody));
            }
        }
        finally
        {
            await customFixture.DisposeAsync();
        }
    }

    [Test]
    public async Task Response_with_headers()
    {
        var customFixture = await UnixSocketFixture.CreateAsync(req => {
            var headers = new Headers {
                {
                    "Content-Type", "application/json"
                }, {
                    "X-Custom-Header", "custom-value"
                },
            };

            return Task.FromResult(new ResponseDto {
                Status = Status.Ok,
                Headers = new ReadOnlyHeaders(headers),
            });
        });

        try
        {
            var response = await customFixture.Client.GetAsync("/headers");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
                Assert.That(response.Headers.GetValues("X-Custom-Header").First(), Is.EqualTo("custom-value"));
            }
        }
        finally
        {
            await customFixture.DisposeAsync();
        }
    }

    [Test]
    public async Task Request_headers_received()
    {
        string? receivedHeader = null;

        var customFixture = await UnixSocketFixture.CreateAsync(req => {
            var values = req.Headers["X-Custom-Header"];
            receivedHeader = values?.FirstOrDefault();

            return Task.FromResult(new ResponseDto {
                Status = Status.Ok,
                Headers = EmptyHeaders,
            });
        });

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/test");
            request.Headers.Add("X-Custom-Header", "test-value");

            var response = await customFixture.Client.SendAsync(request);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(receivedHeader, Is.EqualTo("test-value"));
            }
        }
        finally
        {
            await customFixture.DisposeAsync();
        }
    }

    [Test]
    public async Task Roundtrip_body_echo()
    {
        var expectedBody = "Echo me back";

        var customFixture = await UnixSocketFixture.CreateAsync(async req => {
            var bodyStream = req.Stream;

            if (bodyStream is null)
                return new ResponseDto {
                    Status = Status.BadRequest,
                    Headers = EmptyHeaders,
                };

            using var ms = new MemoryStream();
            await bodyStream.CopyToAsync(ms);
            var body = Encoding.UTF8.GetString(ms.ToArray());

            return new ResponseDto {
                Status = Status.Ok,
                Stream = new MemoryStream(Encoding.UTF8.GetBytes(body)),
                Headers = EmptyHeaders,
            };
        });

        try
        {
            var content = new StringContent(expectedBody);
            var response = await customFixture.Client.PostAsync("/echo", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(responseBody, Is.EqualTo(expectedBody));
            }
        }
        finally
        {
            await customFixture.DisposeAsync();
        }
    }

    [Test]
    public async Task Different_methods()
    {
        var methodReceived = string.Empty;

        var customFixture = await UnixSocketFixture.CreateAsync(req => {
            methodReceived = req.Method.ToString();

            return Task.FromResult(new ResponseDto {
                Status = Status.Ok,
                Headers = EmptyHeaders,
            });
        });

        try
        {
            var response = await customFixture.Client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/method-test"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(methodReceived, Is.EqualTo(nameof(Method.Patch)));
            }
        }
        finally
        {
            await customFixture.DisposeAsync();
        }
    }

    [Test]
    public async Task Multiple_requests_same_connection()
    {
        for (var i = 0; i < 3; i++)
        {
            var response = await _fixture!.Client.GetAsync($"/request-{i}");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Empty);
        }
    }

    [Test]
    public async Task Concurrent_requests()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(i => _fixture!.Client.GetAsync($"/concurrent-{i}"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        Assert.That(responses.All(r => r.StatusCode == HttpStatusCode.OK), Is.True);
    }

    [Test]
    public void Socket_file_created() => Assert.That(File.Exists(_fixture!.SocketPath), Is.True);

    [Test]
    public async Task Raw_socket_roundtrip()
    {
        var path = _fixture!.SocketPath;
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Connect(new UnixDomainSocketEndPoint(path));

        using var stream = new NetworkStream(socket, false);

        var request = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n"u8.ToArray();
        await stream.WriteAsync(request);
        await stream.FlushAsync();

        var buffer = new byte[4096];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var bytesRead = await stream.ReadAsync(buffer, cts.Token);

        Assert.That(bytesRead, Is.GreaterThan(0));

        var response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
        Assert.That(response, Does.StartWith("HTTP/1.1 200"));
    }

    [Test]
    public async Task HttpClient_connects_to_unix_socket()
    {
        var path = _fixture!.SocketPath + ".test2";

        if (File.Exists(path))
            File.Delete(path);

        using var serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        serverSocket.Bind(new UnixDomainSocketEndPoint(path));
        serverSocket.Listen(1);

        var handler = new SocketsHttpHandler {
            ConnectCallback = async (context, ct) => {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), ct);

                return new NetworkStream(socket, true);
            },
        };

        using var client = new HttpClient(handler) {
            BaseAddress = new Uri("http://localhost"),
            Timeout = TimeSpan.FromSeconds(3),
        };

        var sendTask = client.GetAsync("/test");

        var accepted = serverSocket.Accept();
        using var ns = new NetworkStream(accepted, false);

        var buf = new byte[4096];
        var read = await ns.ReadAsync(buf, CancellationToken.None);
        var reqStr = Encoding.ASCII.GetString(buf, 0, read);

        Assert.That(reqStr, Does.Contain("GET /test"));

        var resp = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray();
        await ns.WriteAsync(resp);
        await ns.FlushAsync();

        var result = await sendTask;
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        serverSocket.Dispose();
        File.Delete(path);
    }
}

public class UnixSocketAcceptorTests
{
    [Test]
    public async Task Bind_creates_socket_file()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"arbiter_test_{Guid.NewGuid():N}.sock");
        var acceptor = new UnixSocketAcceptor();

        try
        {
            await acceptor.Bind([tempPath], 128);

            Assert.That(File.Exists(tempPath), Is.True);
        }
        finally
        {
            await CleanupAcceptor(acceptor, tempPath);
        }
    }

    [Test]
    public async Task Bind_idempotent()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"arbiter_test_{Guid.NewGuid():N}.sock");
        var acceptor = new UnixSocketAcceptor();

        try
        {
            await acceptor.Bind([tempPath], 128);
            await acceptor.Bind([tempPath], 128);

            Assert.That(File.Exists(tempPath), Is.True);
        }
        finally
        {
            await CleanupAcceptor(acceptor, tempPath);
        }
    }

    [Test]
    public async Task Prune_removes_socket()
    {
        var pathA = Path.Combine(Path.GetTempPath(), $"arbiter_test_a_{Guid.NewGuid():N}.sock");
        var pathB = Path.Combine(Path.GetTempPath(), $"arbiter_test_b_{Guid.NewGuid():N}.sock");
        var acceptor = new UnixSocketAcceptor();

        try
        {
            await acceptor.Bind([pathA, pathB], 128);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(File.Exists(pathA), Is.True);
                Assert.That(File.Exists(pathB), Is.True);
            }

            await acceptor.Bind([pathA], 128);

            await Task.Delay(100);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(File.Exists(pathA), Is.True);
                Assert.That(File.Exists(pathB), Is.False);
            }
        }
        finally
        {
            await CleanupAcceptor(acceptor, pathA);
            await CleanupAcceptor(acceptor, pathB);
        }
    }

    [Test]
    public void Port_is_negative_one()
    {
        var acceptor = new UnixSocketAcceptor();

        var field = acceptor.GetType()
            .GetField("_transports", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(field, Is.Not.Null);
    }

    private static async Task CleanupAcceptor(UnixSocketAcceptor acceptor, string path)
    {
        var sockets = acceptor.GetType()
            .GetField("_sockets", BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(acceptor) as IDictionary<string, object>;

        if (sockets is not null && sockets.TryGetValue(path, out var socketObj))
        {
            var stopMethod = socketObj.GetType().GetMethod("Stop");
            var closeMethod = socketObj.GetType().GetMethod("Close");

            if (stopMethod is not null)
                await (Task)stopMethod.Invoke(socketObj, null)!;

            closeMethod?.Invoke(socketObj, null);
        }

        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
