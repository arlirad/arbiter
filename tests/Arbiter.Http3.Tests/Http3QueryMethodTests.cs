using System.Net;
using System.Net.Quic;
using System.Runtime.Versioning;
using System.Text;
using Arbiter.Core.Enums;
using Arbiter.Http3.Tests.Helpers;
using Arbiter.Infrastructure.Middleware;
using Arbiter.Protocol.Http3;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3QueryMethodTests
{
    private CancellationTokenSource _cts = null!;
    private Http3IntegrationFixture _fixture = null!;

    [SetUp]
    public async Task SetUp()
    {
        if (!QuicListener.IsSupported)
            Assert.Ignore("QUIC is not supported on this platform");

        _fixture = await Http3IntegrationFixture.CreateAsync();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }

    [Test]
    public async Task QUERY_with_body_reaches_server()
    {
        var fixture = _fixture;
        var requestBody = "Hello, QUERY!"u8.ToArray();

        var requestStream = await fixture.CreateClientRequestStreamAsync();

        await requestStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["QUERY"],
            [":path"] = ["/query"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
        });

        await requestStream.WriteAsync(requestBody);
        await requestStream.FinishAsync();

        var serverStream = await fixture.AcceptRequestStream(CancellationToken.None);

        foreach (var _ in await serverStream.ReadHeaders())
        {
        }

        var buffer = new byte[100];
        var bytesRead = await serverStream.ReadAsync(buffer);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bytesRead, Is.EqualTo(requestBody.Length));
            Assert.That(Encoding.UTF8.GetString(buffer, 0, bytesRead), Is.EqualTo("Hello, QUERY!"));
        }
    }

    [Test]
    public async Task GetRequest_parses_query_method()
    {
        var clientStream = await _fixture.CreateClientRequestStreamAsync(_cts.Token);

        await clientStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["QUERY"],
            [":scheme"] = ["https"],
            [":authority"] = ["localhost"],
            [":path"] = ["/q"],
        });

        await clientStream.FinishAsync(_cts.Token);

        var serverStream = await _fixture.AcceptRequestStream(_cts.Token);
        var transaction = new Http3Transaction(new TransactionIdProvider(), serverStream, _fixture.Port, IPAddress.Loopback);
        var request = await transaction.GetRequest(_cts.Token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.Method, Is.EqualTo(Method.Query));
            Assert.That(request.Path, Is.EqualTo("/q"));
            Assert.That(request.Upgrade, Is.Null);
        }
    }
}
