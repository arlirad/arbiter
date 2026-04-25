using System.Net;
using System.Net.Quic;
using System.Runtime.Versioning;
using System.Text;
using Arbiter.Application.DTOs;
using Arbiter.Core.Enums;
using Arbiter.Http3.Tests.Helpers;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class HttpClientIntegrationTests
{
    private HttpClientServerFixture? _fixture;

    [SetUp]
    public async Task SetUp()
    {
        if (!QuicListener.IsSupported)
            Assert.Ignore("QUIC is not supported on this platform");

        _fixture = await HttpClientServerFixture.CreateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_fixture is not null)
        {
            try
            {
                await _fixture.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
            }

            _fixture = null;
        }
    }

    [Test]
    public async Task Single_GET_returns_200()
    {
        var fixture = _fixture!;
        var response = await fixture.Client.GetAsync("/");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Empty);
    }

    [Test]
    public async Task Multiple_GETs_same_connection()
    {
        var fixture = _fixture!;

        for (var i = 0; i < 3; i++)
        {
            var response = await fixture.Client.GetAsync($"/request-{i}");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Empty);
        }

        Assert.Pass();
    }

    [Test]
    public async Task POST_with_body()
    {
        var fixture = _fixture!;
        var requestBody = "Hello, HTTP/3!";
        var content = new StringContent(requestBody, Encoding.UTF8, "text/plain");

        var response = await fixture.Client.PostAsync("/upload", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Response_with_body()
    {
        var expectedBody = "Response from server";

        var customFixture = await HttpClientServerFixture.CreateAsync(req => Task.FromResult(new ResponseDto {
            Status = Status.Ok,
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(expectedBody)),
        }));

        try
        {
            var response = await customFixture.Client.GetAsync("/data");
            var body = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Is.EqualTo(expectedBody));
        }
        finally
        {
            await customFixture.DisposeAsync();
        }
    }

    [Test]
    public async Task Response_with_multiple_headers()
    {
        var customFixture = await HttpClientServerFixture.CreateAsync(req => {
            var headers = new Arbiter.Core.ValueObjects.Headers();
            headers.Add("Content-Type", "application/json");
            headers.Add("X-Custom-Header", "custom-value");
            headers.Add("Cache-Control", "no-cache");

            return Task.FromResult(new ResponseDto {
                Status = Status.Ok,
                Headers = new Core.ValueObjects.ReadOnlyHeaders(headers),
            });
        });

        try
        {
            var response = await customFixture.Client.GetAsync("/headers");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
            Assert.That(response.Headers.GetValues("X-Custom-Header").First(), Is.EqualTo("custom-value"));
        }
        finally
        {
            await customFixture.DisposeAsync();
        }
    }

    [Test]
    public async Task Request_with_custom_headers()
    {
        string? receivedCustomHeader = null;

        var customFixture = await HttpClientServerFixture.CreateAsync(req => {
            var values = req.Headers["X-Custom-Header"];
            receivedCustomHeader = values?.FirstOrDefault();
            return Task.FromResult(new ResponseDto {
                Status = Status.Ok
            });
        });

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/test") {
                Version = System.Net.HttpVersion.Version30,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            };
            request.Headers.Add("X-Custom-Header", "test-value");

            var response = await customFixture.Client.SendAsync(request);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(receivedCustomHeader, Is.EqualTo("test-value"));
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

        var customFixture = await HttpClientServerFixture.CreateAsync(async req => {
            var bodyStream = req.Stream;
            if (bodyStream is null)
            {
                return new ResponseDto {
                    Status = Status.BadRequest
                };
            }

            using var ms = new MemoryStream();
            await bodyStream.CopyToAsync(ms);
            var body = Encoding.UTF8.GetString(ms.ToArray());

            return new ResponseDto {
                Status = Status.Ok,
                Stream = new MemoryStream(Encoding.UTF8.GetBytes(body)),
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

        var customFixture = await HttpClientServerFixture.CreateAsync(req => {
            methodReceived = req.Method.ToString();
            return Task.FromResult(new ResponseDto {
                Status = Status.Ok
            });
        });

        try
        {
            var response = await customFixture.Client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/method-test") {
                Version = System.Net.HttpVersion.Version30,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            });
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
}