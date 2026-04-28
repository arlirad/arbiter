using System.Net;
using System.Runtime.Versioning;
using Arbiter.Http3.Tests.Helpers;
using Arlirad.Http3;
using Arlirad.Http3.Streams;
using Arlirad.WebSocket;

namespace Arbiter.Http3.Tests;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class H3WebSocketIngressTests
{
    private Http3IntegrationFixture _fixture = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public async Task SetUp()
    {
        if (!System.Net.Quic.QuicListener.IsSupported)
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

    private async Task SendConnectHeaders(Http3RequestStream clientStream, string path = "/ws")
    {
        await clientStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["CONNECT"],
            [":scheme"] = ["https"],
            [":authority"] = [$"localhost:{_fixture.Port}"],
            [":path"] = [path],
            [":protocol"] = ["websocket"],
        });
        await clientStream.WriteAsync(ReadOnlyMemory<byte>.Empty, _cts.Token);
    }

    [Test]
    public async Task GetRequest_detects_websocket_upgrade()
    {
        var clientStream = await _fixture.CreateClientRequestStreamAsync(_cts.Token);
        await SendConnectHeaders(clientStream);

        var serverStream = await _fixture.AcceptRequestStream(_cts.Token);
        var transaction = new Http3Transaction(serverStream, _fixture.Port, IPAddress.Loopback);
        var request = await transaction.GetRequest();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.Upgrade, Is.AssignableTo<Arbiter.Core.Interfaces.IWebSocketUpgrade>());
            Assert.That(request.Method, Is.EqualTo(Arbiter.Core.Enums.Method.Get));
            Assert.That(request.Path, Is.EqualTo("/ws"));
        }
    }

    [Test]
    public async Task GetRequest_non_CONNECT_has_no_websocket_upgrade()
    {
        var clientStream = await _fixture.CreateClientRequestStreamAsync(_cts.Token);
        await clientStream.WriteHeaders(new Dictionary<string, List<string>> {
            [":method"] = ["GET"],
            [":scheme"] = ["https"],
            [":authority"] = [$"localhost:{_fixture.Port}"],
            [":path"] = ["/api/hello"],
        });
        await clientStream.FinishAsync(_cts.Token);

        var serverStream = await _fixture.AcceptRequestStream(_cts.Token);
        var transaction = new Http3Transaction(serverStream, _fixture.Port, IPAddress.Loopback);
        var request = await transaction.GetRequest();

        Assert.That(request!.Upgrade, Is.Null);
    }

    [Test]
    public async Task AcceptAsync_writes_200_status()
    {
        var clientStream = await _fixture.CreateClientRequestStreamAsync(_cts.Token);
        await SendConnectHeaders(clientStream);

        var serverStream = await _fixture.AcceptRequestStream(_cts.Token);
        var transaction = new Http3Transaction(serverStream, _fixture.Port, IPAddress.Loopback);
        var request = await transaction.GetRequest();

        Assert.That(request!.Upgrade, Is.AssignableTo<Arbiter.Core.Interfaces.IWebSocketUpgrade>());

        await request.Upgrade!.AcceptAsync();

        var responseHeaders = new List<KeyValuePair<string, string>>();
        await foreach (var header in clientStream.ReadHeaders(_cts.Token))
            responseHeaders.Add(new KeyValuePair<string, string>(header.Key, header.Value ?? string.Empty));

        var statusHeader = responseHeaders.FirstOrDefault(h => h.Key == ":status");
        Assert.That(statusHeader.Value, Is.EqualTo("200"));
    }

    [Test]
    public async Task Full_roundtrip_connect_then_websocket_frames()
    {
        var clientStream = await _fixture.CreateClientRequestStreamAsync(_cts.Token);
        await SendConnectHeaders(clientStream);

        var serverStream = await _fixture.AcceptRequestStream(_cts.Token);
        var transaction = new Http3Transaction(serverStream, _fixture.Port, IPAddress.Loopback);
        var request = await transaction.GetRequest();
        var relayStream = await request!.Upgrade!.AcceptAsync();

        await foreach (var _ in clientStream.ReadHeaders(_cts.Token))
        {
        }

        var serverWsTask = Task.Run(async () => {
            await using var ws = new WebSocketConnection(relayStream);
            var msg = await ws.ReceiveTextAsync(_cts.Token);
            Assert.That(msg, Is.EqualTo("hello h3"));
            await ws.SendTextAsync("hi from h3", _cts.Token);
        }, _cts.Token);

        await using var clientWs = new WebSocketConnection(clientStream);
        await clientWs.SendTextAsync("hello h3", _cts.Token);
        var reply = await clientWs.ReceiveTextAsync(_cts.Token);
        Assert.That(reply, Is.EqualTo("hi from h3"));

        await serverWsTask;
    }

    [Test]
    public async Task Binary_frame_roundtrip()
    {
        var clientStream = await _fixture.CreateClientRequestStreamAsync(_cts.Token);
        await SendConnectHeaders(clientStream);

        var serverStream = await _fixture.AcceptRequestStream(_cts.Token);
        var transaction = new Http3Transaction(serverStream, _fixture.Port, IPAddress.Loopback);
        var request = await transaction.GetRequest();
        var relayStream = await request!.Upgrade!.AcceptAsync();

        await foreach (var _ in clientStream.ReadHeaders(_cts.Token))
        {
        }

        var serverWsTask = Task.Run(async () => {
            await using var ws = new WebSocketConnection(relayStream);
            var msg = await ws.ReceiveBinaryAsync(_cts.Token);
            await ws.SendBinaryAsync(msg.ToArray(), _cts.Token);
        }, _cts.Token);

        await using var clientWs = new WebSocketConnection(clientStream);
        var data = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        await clientWs.SendBinaryAsync(data, _cts.Token);
        var reply = await clientWs.ReceiveBinaryAsync(_cts.Token);
        Assert.That(reply.ToArray(), Is.EqualTo(data));

        await serverWsTask;
    }

    [Test]
    public async Task Close_handshake()
    {
        var clientStream = await _fixture.CreateClientRequestStreamAsync(_cts.Token);
        await SendConnectHeaders(clientStream);

        var serverStream = await _fixture.AcceptRequestStream(_cts.Token);
        var transaction = new Http3Transaction(serverStream, _fixture.Port, IPAddress.Loopback);
        var request = await transaction.GetRequest();
        var relayStream = await request!.Upgrade!.AcceptAsync();

        await foreach (var _ in clientStream.ReadHeaders(_cts.Token))
        {
        }

        var serverWsTask = Task.Run(async () => {
            await using var ws = new WebSocketConnection(relayStream);
            var msg = await ws.ReceiveAsync(_cts.Token);
            Assert.That(msg.Opcode, Is.EqualTo(WebSocketOpcode.Close));
        }, _cts.Token);

        await using var clientWs = new WebSocketConnection(clientStream);
        await clientWs.CloseAsync(WebSocketCloseStatusCode.Normal, "bye", _cts.Token);

        await serverWsTask;
    }

    [Test]
    public async Task Server_initiated_message()
    {
        var clientStream = await _fixture.CreateClientRequestStreamAsync(_cts.Token);
        await SendConnectHeaders(clientStream);

        var serverStream = await _fixture.AcceptRequestStream(_cts.Token);
        var transaction = new Http3Transaction(serverStream, _fixture.Port, IPAddress.Loopback);
        var request = await transaction.GetRequest();
        var relayStream = await request!.Upgrade!.AcceptAsync();

        await foreach (var _ in clientStream.ReadHeaders(_cts.Token))
        {
        }

        var serverWsTask = Task.Run(async () => {
            await using var ws = new WebSocketConnection(relayStream);
            await ws.SendTextAsync("push from h3", _cts.Token);
            var msg = await ws.ReceiveAsync(_cts.Token);
            Assert.That(msg.Opcode, Is.EqualTo(WebSocketOpcode.Close));
        }, _cts.Token);

        await using var clientWs = new WebSocketConnection(clientStream);
        var msg = await clientWs.ReceiveTextAsync(_cts.Token);
        Assert.That(msg, Is.EqualTo("push from h3"));
        await clientWs.CloseAsync(WebSocketCloseStatusCode.Normal, ct: _cts.Token);

        await serverWsTask;
    }
}
