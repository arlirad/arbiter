using System.Net;
using System.Net.Sockets;
using System.Text;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Middleware;
using Arbiter.Protocol.WebSocket;

namespace Arbiter.Protocol.Http11.Tests;

[TestFixture]
public class Http11WebSocketIngressTests
{
    [SetUp]
    public void SetUp() => _cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    [TearDown]
    public void TearDown()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private CancellationTokenSource _cts = null!;

    private static (Stream serverStream, Stream clientStream) CreateStreamPair()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var connectTask = clientSocket.ConnectAsync(listener.LocalEndPoint!);
        var serverSocket = listener.Accept();
        listener.Dispose();
        connectTask.GetAwaiter().GetResult();

        return (new NetworkStream(serverSocket, true), new NetworkStream(clientSocket, true));
    }

    [Test]
    public async Task GetRequest_detects_websocket_upgrade()
    {
        var (serverStream, clientStream) = CreateStreamPair();

        await using (serverStream)
        await using (clientStream)
        {
            var upgradeRequest = "GET /ws HTTP/1.1\r\nHost: localhost\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n"u8.ToArray();
            await clientStream.WriteAsync(upgradeRequest, _cts.Token);
            await clientStream.FlushAsync(_cts.Token);

            var transaction = new Http11Transaction(new TransactionIdProvider(), serverStream, false, 80, IPAddress.Loopback);
            var request = await transaction.GetRequest();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(request, Is.Not.Null);
                Assert.That(request!.Upgrade, Is.AssignableTo<IWebSocketUpgrade>());
                Assert.That(request.Method, Is.EqualTo(Method.Get));
                Assert.That(request.Path, Is.EqualTo("/ws"));
            }
        }
    }

    [Test]
    public async Task GetRequest_non_upgrade_request_has_no_websocket_upgrade()
    {
        var (serverStream, clientStream) = CreateStreamPair();

        await using (serverStream)
        await using (clientStream)
        {
            var normalRequest = "GET /api/hello HTTP/1.1\r\nHost: localhost\r\n\r\n"u8.ToArray();
            await clientStream.WriteAsync(normalRequest, _cts.Token);
            await clientStream.FlushAsync(_cts.Token);

            var transaction = new Http11Transaction(new TransactionIdProvider(), serverStream, false, 80, IPAddress.Loopback);
            var request = await transaction.GetRequest();

            Assert.That(request!.Upgrade, Is.Null);
        }
    }

    [Test]
    public async Task AcceptAsync_writes_101_response()
    {
        var (serverStream, clientStream) = CreateStreamPair();

        await using (serverStream)
        await using (clientStream)
        {
            var upgradeRequest = "GET /ws HTTP/1.1\r\nHost: localhost\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n"u8.ToArray();
            await clientStream.WriteAsync(upgradeRequest, _cts.Token);
            await clientStream.FlushAsync(_cts.Token);

            var transaction = new Http11Transaction(new TransactionIdProvider(), serverStream, false, 80, IPAddress.Loopback);
            var request = await transaction.GetRequest();
            var upgrade = (IWebSocketUpgrade)request!.Upgrade!;

            var headers = new Headers {
                {
                    "Upgrade", "websocket"
                }, {
                    "Connection", "upgrade"
                },
            };

            await upgrade.AcceptAsync(new ReadOnlyHeaders(headers));

            var buffer = new byte[256];
            var read = await clientStream.ReadAsync(buffer.AsMemory(), _cts.Token);
            var response = Encoding.UTF8.GetString(buffer.AsSpan(0, read));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response, Does.StartWith("HTTP/1.1 101"));
                Assert.That(response, Does.Contain("Upgrade: websocket"));
                Assert.That(response, Does.Contain("Connection: upgrade"));
            }
        }
    }

    [Test]
    public async Task Full_roundtrip_upgrade_then_websocket_frames()
    {
        var (serverStream, clientStream) = CreateStreamPair();

        await using (serverStream)
        await using (clientStream)
        {
            var upgradeRequest = "GET /ws HTTP/1.1\r\nHost: localhost\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n"u8.ToArray();
            await clientStream.WriteAsync(upgradeRequest, _cts.Token);
            await clientStream.FlushAsync(_cts.Token);

            var transaction = new Http11Transaction(new TransactionIdProvider(), serverStream, false, 80, IPAddress.Loopback);
            var request = await transaction.GetRequest();

            Assert.That(request!.Upgrade, Is.AssignableTo<IWebSocketUpgrade>());

            var relayStream = await request.Upgrade!.AcceptAsync();

            var buffer = new byte[256];
            var read = await clientStream.ReadAsync(buffer.AsMemory(), _cts.Token);
            var responseHeader = Encoding.UTF8.GetString(buffer.AsSpan(0, read));
            Assert.That(responseHeader, Does.StartWith("HTTP/1.1 101"));

            var serverWsTask = Task.Run(async () => {
                await using var ws = new WebSocketConnection(relayStream);
                var msg = await ws.ReceiveTextAsync(_cts.Token);
                Assert.That(msg, Is.EqualTo("ping"));
                await ws.SendTextAsync("pong", _cts.Token);
                await ws.DisposeAsync();
            }, _cts.Token);

            await using var clientWs = new WebSocketConnection(clientStream);
            await clientWs.SendTextAsync("ping", _cts.Token);
            var reply = await clientWs.ReceiveTextAsync(_cts.Token);
            Assert.That(reply, Is.EqualTo("pong"));
            await clientWs.DisposeAsync();

            await serverWsTask;
        }
    }

    [Test]
    public async Task Binary_frame_roundtrip_after_upgrade()
    {
        var (serverStream, clientStream) = CreateStreamPair();

        await using (serverStream)
        await using (clientStream)
        {
            var upgradeRequest = "GET /ws HTTP/1.1\r\nHost: localhost\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n"u8.ToArray();
            await clientStream.WriteAsync(upgradeRequest, _cts.Token);
            await clientStream.FlushAsync(_cts.Token);

            var transaction = new Http11Transaction(new TransactionIdProvider(), serverStream, false, 80, IPAddress.Loopback);
            var request = await transaction.GetRequest();
            var relayStream = await request!.Upgrade!.AcceptAsync();

            var buffer = new byte[256];
            var read = await clientStream.ReadAsync(buffer.AsMemory(), _cts.Token);
            var responseHeader = Encoding.UTF8.GetString(buffer.AsSpan(0, read));
            Assert.That(responseHeader, Does.StartWith("HTTP/1.1 101"));

            var serverWsTask = Task.Run(async () => {
                await using var ws = new WebSocketConnection(relayStream);
                var msg = await ws.ReceiveBinaryAsync(_cts.Token);
                await ws.SendBinaryAsync(msg.ToArray(), _cts.Token);
                await ws.DisposeAsync();
            }, _cts.Token);

            await using var clientWs = new WebSocketConnection(clientStream);

            var data = new byte[] {
                0xCA, 0xFE, 0xBA, 0xBE,
            };

            await clientWs.SendBinaryAsync(data, _cts.Token);
            var reply = await clientWs.ReceiveBinaryAsync(_cts.Token);
            Assert.That(reply.ToArray(), Is.EqualTo(data));
            await clientWs.DisposeAsync();

            await serverWsTask;
        }
    }

    [Test]
    public async Task Close_handshake_after_upgrade()
    {
        var (serverStream, clientStream) = CreateStreamPair();

        await using (serverStream)
        await using (clientStream)
        {
            var upgradeRequest = "GET /ws HTTP/1.1\r\nHost: localhost\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n"u8.ToArray();
            await clientStream.WriteAsync(upgradeRequest, _cts.Token);
            await clientStream.FlushAsync(_cts.Token);

            var transaction = new Http11Transaction(new TransactionIdProvider(), serverStream, false, 80, IPAddress.Loopback);
            var request = await transaction.GetRequest();
            var relayStream = await request!.Upgrade!.AcceptAsync();

            var buffer = new byte[256];
            var read = await clientStream.ReadAsync(buffer.AsMemory(), _cts.Token);
            var responseHeader = Encoding.UTF8.GetString(buffer.AsSpan(0, read));
            Assert.That(responseHeader, Does.StartWith("HTTP/1.1 101"));

            var serverWsTask = Task.Run(async () => {
                await using var ws = new WebSocketConnection(relayStream);
                var msg = await ws.ReceiveAsync(_cts.Token);
                Assert.That(msg.Opcode, Is.EqualTo(WebSocketOpcode.Close));
                await ws.DisposeAsync();
            }, _cts.Token);

            await using var clientWs = new WebSocketConnection(clientStream);
            await clientWs.CloseAsync(WebSocketCloseStatusCode.Normal, "bye", _cts.Token);
            await clientWs.DisposeAsync();

            await serverWsTask;
        }
    }

    [Test]
    public async Task Server_initiated_message_after_upgrade()
    {
        var (serverStream, clientStream) = CreateStreamPair();

        await using (serverStream)
        await using (clientStream)
        {
            var upgradeRequest = "GET /ws HTTP/1.1\r\nHost: localhost\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n"u8.ToArray();
            await clientStream.WriteAsync(upgradeRequest, _cts.Token);
            await clientStream.FlushAsync(_cts.Token);

            var transaction = new Http11Transaction(new TransactionIdProvider(), serverStream, false, 80, IPAddress.Loopback);
            var request = await transaction.GetRequest();
            var relayStream = await request!.Upgrade!.AcceptAsync();

            var buffer = new byte[256];
            var read = await clientStream.ReadAsync(buffer.AsMemory(), _cts.Token);
            var responseHeader = Encoding.UTF8.GetString(buffer.AsSpan(0, read));
            Assert.That(responseHeader, Does.StartWith("HTTP/1.1 101"));

            var serverWsTask = Task.Run(async () => {
                await using var ws = new WebSocketConnection(relayStream);
                await ws.SendTextAsync("push from server", _cts.Token);
                await ws.ReceiveAsync(_cts.Token);
                await ws.DisposeAsync();
            }, _cts.Token);

            await using var clientWs = new WebSocketConnection(clientStream);
            var msg = await clientWs.ReceiveTextAsync(_cts.Token);
            Assert.That(msg, Is.EqualTo("push from server"));
            await clientWs.DisposeAsync();

            await serverWsTask;
        }
    }
}
