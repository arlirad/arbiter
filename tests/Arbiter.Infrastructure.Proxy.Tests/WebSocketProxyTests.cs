using System.Net;
using System.Net.Sockets;
using System.Text;
using Arbiter.Protocol.WebSocket;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Proxy;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Infrastructure.Proxy.Tests;

[TestFixture]
public class WebSocketProxyTests
{
    private static readonly ContextFactory _contextFactory = new();

#pragma warning disable NUnit1032
    private TcpListener _backendListener = null!;
#pragma warning restore NUnit1032

    private ProxyMiddleware _proxy = null!;
    private int _backendPort;
    private CancellationTokenSource _cts = null!;

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
        _backendListener = new TcpListener(IPAddress.Loopback, 0);
        _backendListener.Start();
        _backendPort = ((IPEndPoint)_backendListener.LocalEndpoint).Port;
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        _proxy = new ProxyMiddleware();
        var configDict = new Dictionary<string, string?> { { "Target", $"http://localhost:{_backendPort}" } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();
        await _proxy.Configure(new ComponentDataContainer(), config);
    }

    [TearDown]
    public void TearDown()
    {
        _cts.Cancel();
        _cts.Dispose();
        _backendListener.Stop();
    }

    private Task StartBackend(Func<Stream, CancellationToken, Task> handleWebSocket)
    {
        return Task.Run(async () =>
        {
            var client = await _backendListener.AcceptTcpClientAsync(_cts.Token);
            var stream = client.GetStream();

            try
            {
                await ReadUpgradeRequest(stream, _cts.Token);
                await Write101Response(stream);
                await handleWebSocket(stream, _cts.Token);
            }
            finally
            {
                await stream.DisposeAsync();
                client.Dispose();
            }
        }, _cts.Token);
    }

    private static async Task<string> ReadUpgradeRequest(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);
            if (read == 0)
                break;

            totalRead += read;

            if (buffer.AsSpan(0, totalRead).EndsWith("\r\n\r\n"u8))
                return Encoding.ASCII.GetString(buffer, 0, totalRead);
        }

        return Encoding.ASCII.GetString(buffer, 0, totalRead);
    }

    private static async Task Write101Response(Stream stream)
    {
        var response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: upgrade\r\n\r\n"u8.ToArray();
        await stream.WriteAsync(response);
        await stream.FlushAsync();
    }

    private static Context CreateContext(IWebSocketUpgrade upgrade, string path = "/ws")
    {
        var headers = new Dictionary<string, List<string>> {
            { "Host", ["localhost"] },
            { "Upgrade", ["websocket"] },
            { "Connection", ["Upgrade"] },
            { "Sec-WebSocket-Key", ["dGhlIHNhbXBsZSBub25jZQ=="] },
            { "Sec-WebSocket-Version", ["13"] },
        };

        return _contextFactory.Create(
            1,
            Method.Get,
            path,
            headers,
            null,
            upgrade,
            "localhost",
            false,
            null
        )!;
    }

    private static Context CreateHttp3WebSocketContext(IWebSocketUpgrade upgrade, string path = "/ws")
    {
        return _contextFactory.Create(
            1,
            Method.Get,
            path,
            new Dictionary<string, List<string>>(),
            null,
            upgrade,
            "localhost",
            true,
            null
        )!;
    }

    private static Context CreateContext(IUpgrade upgrade)
    {
        return _contextFactory.Create(
            1,
            Method.Get,
            "/upgrade",
            new Dictionary<string, List<string>>(),
            null,
            upgrade,
            "localhost",
            false,
            null
        )!;
    }

    private sealed class MockWebSocketUpgrade : IWebSocketUpgrade
    {
        private readonly Socket _proxySocket;
        private readonly Socket _testSocket;

        public Stream TestSideStream
        {
            get;
        }

        public MockWebSocketUpgrade()
        {
            _proxySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            _proxySocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            _testSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));

            _proxySocket.Listen(1);
            var connectTask = _testSocket.ConnectAsync(_proxySocket.LocalEndPoint!);

            var accepted = _proxySocket.Accept();
            _proxySocket.Close();
            _proxySocket = accepted;

            connectTask.GetAwaiter().GetResult();

            TestSideStream = new NetworkStream(_testSocket, ownsSocket: true);
        }

        public Task<Stream> AcceptAsync(ReadOnlyHeaders? responseHeaders = null)
        {
            return Task.FromResult<Stream>(new NetworkStream(_proxySocket, ownsSocket: true));
        }
    }

    private sealed class MockUpgrade : IUpgrade
    {
        public Task<Stream> AcceptAsync(ReadOnlyHeaders? responseHeaders = null)
        {
            throw new NotSupportedException();
        }
    }

    [Test]
    public async Task HandleWebSocket_sets_IsUpgraded()
    {
        var mockUpgrade = new MockWebSocketUpgrade();
        var context = CreateContext(mockUpgrade);
        var backendTask = StartBackend(async (stream, ct) =>
        {
            await Task.Delay(200, ct);
        });

        await _proxy.Handle(context);

        Assert.That(context.IsUpgraded, Is.True);
    }

    [Test]
    public async Task HandleWebSocket_synthesizes_http11_handshake_for_http3_connect()
    {
        var mockUpgrade = new MockWebSocketUpgrade();
        var context = CreateHttp3WebSocketContext(mockUpgrade);

        var backendTask = Task.Run(async () =>
        {
            var client = await _backendListener.AcceptTcpClientAsync(_cts.Token);
            await using var stream = client.GetStream();

            try
            {
                var request = await ReadUpgradeRequest(stream, _cts.Token);

                Assert.Multiple(() =>
                {
                    Assert.That(request, Does.StartWith("GET /ws HTTP/1.1\r\n"));
                    Assert.That(request, Does.Contain("Upgrade: websocket\r\n"));
                    Assert.That(request, Does.Contain("Connection: Upgrade\r\n"));
                    Assert.That(request, Does.Contain("Sec-WebSocket-Version: 13\r\n"));
                    Assert.That(request, Does.Match("Sec-WebSocket-Key: [A-Za-z0-9+/]{22}==\r\n"));
                });

                var response = "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n"u8.ToArray();
                await stream.WriteAsync(response, _cts.Token);
                await stream.FlushAsync(_cts.Token);
            }
            finally
            {
                client.Dispose();
            }
        }, _cts.Token);

        await _proxy.Handle(context);
        await backendTask;

        Assert.That(context.Response.Status, Is.EqualTo(Status.Forbidden));
    }

    [Test]
    public async Task HandleWebSocket_forwards_query_string_to_backend()
    {
        var mockUpgrade = new MockWebSocketUpgrade();
        var context = CreateContext(mockUpgrade, "/socket?api_key=test-token&deviceId=abc");

        var backendTask = Task.Run(async () =>
        {
            var client = await _backendListener.AcceptTcpClientAsync(_cts.Token);
            await using var stream = client.GetStream();

            try
            {
                var request = await ReadUpgradeRequest(stream, _cts.Token);
                Assert.That(request, Does.StartWith("GET /socket?api_key=test-token&deviceId=abc HTTP/1.1\r\n"));

                var response = "HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\n\r\n"u8.ToArray();
                await stream.WriteAsync(response, _cts.Token);
                await stream.FlushAsync(_cts.Token);
            }
            finally
            {
                client.Dispose();
            }
        }, _cts.Token);

        await _proxy.Handle(context);
        await backendTask;

        Assert.That(context.Response.Status, Is.EqualTo(Status.Unauthorized));
    }

    [Test]
    public async Task Handle_returns_NotImplemented_for_unknown_upgrade_type()
    {
        var context = CreateContext(new MockUpgrade());

        await _proxy.Handle(context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.NotImplemented));
        Assert.That(context.IsUpgraded, Is.False);
    }

    [Test]
    public async Task HandleWebSocket_relays_text_frames_bidirectionally()
    {
        var mockUpgrade = new MockWebSocketUpgrade();
        var context = CreateContext(mockUpgrade);
        var backendTask = StartBackend(async (stream, ct) =>
        {
            var reader = new WebSocketFrameReader(stream);
            var writer = new WebSocketFrameWriter(stream);

            for (var i = 0; i < 2; i++)
            {
                var frame = await reader.ReadFrame(ct);
                await writer.WriteFrame(frame.Opcode, frame.Fin, frame.Payload, ct);
            }

            await writer.WriteClose(WebSocketCloseStatusCode.Normal, ct: ct);
        });

        var proxyTask = Task.Run(() => _proxy.Handle(context), _cts.Token);

        await using var clientConn = new WebSocketConnection(mockUpgrade.TestSideStream);

        await clientConn.SendTextAsync("hello", _cts.Token);
        var echo1 = await clientConn.ReceiveTextAsync(_cts.Token);
        Assert.That(echo1, Is.EqualTo("hello"));

        await clientConn.SendTextAsync("world", _cts.Token);
        var echo2 = await clientConn.ReceiveTextAsync(_cts.Token);
        Assert.That(echo2, Is.EqualTo("world"));

        var close = await clientConn.ReceiveAsync(_cts.Token);
        Assert.That(close.Opcode, Is.EqualTo(WebSocketOpcode.Close));

        await clientConn.DisposeAsync();
        await proxyTask;
        await backendTask;
    }

    [Test]
    public async Task HandleWebSocket_relays_binary_frames()
    {
        var mockUpgrade = new MockWebSocketUpgrade();
        var context = CreateContext(mockUpgrade);
        var backendTask = StartBackend(async (stream, ct) =>
        {
            var reader = new WebSocketFrameReader(stream);
            var writer = new WebSocketFrameWriter(stream);

            var frame = await reader.ReadFrame(ct);
            await writer.WriteFrame(frame.Opcode, frame.Fin, frame.Payload, ct);
            await writer.WriteClose(WebSocketCloseStatusCode.Normal, ct: ct);
        });

        var proxyTask = Task.Run(() => _proxy.Handle(context), _cts.Token);

        await using var clientConn = new WebSocketConnection(mockUpgrade.TestSideStream);

        var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        await clientConn.SendBinaryAsync(data, _cts.Token);
        var echo = await clientConn.ReceiveBinaryAsync(_cts.Token);
        Assert.That(echo.ToArray(), Is.EqualTo(data));

        await clientConn.DisposeAsync();
        await proxyTask;
        await backendTask;
    }

    [Test]
    public async Task HandleWebSocket_returns_backend_status_on_backend_rejection()
    {
        var mockUpgrade = new MockWebSocketUpgrade();
        var context = CreateContext(mockUpgrade);

        var backendTask = Task.Run(async () =>
        {
            var client = await _backendListener.AcceptTcpClientAsync(_cts.Token);
            var stream = client.GetStream();

            await ReadUpgradeRequest(stream, _cts.Token);

            var response = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"backend\"\r\nContent-Type: text/plain\r\nContent-Length: 12\r\n\r\nunauthorized"u8.ToArray();
            await stream.WriteAsync(response);
            await stream.FlushAsync();

            await stream.DisposeAsync();
            client.Dispose();
        }, _cts.Token);

        await _proxy.Handle(context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.Unauthorized));
        Assert.That(context.Response.Headers["WWW-Authenticate"], Is.EqualTo(new List<string> { "Basic realm=\"backend\"" }));

        Assert.That(context.Response.Stream, Is.Not.Null);
        using var reader = new StreamReader(context.Response.Stream!, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(_cts.Token);
        Assert.That(body, Is.EqualTo("unauthorized"));
        Assert.That(context.IsUpgraded, Is.False);
    }

    [Test]
    public async Task HandleWebSocket_relays_close_frame()
    {
        var mockUpgrade = new MockWebSocketUpgrade();
        var context = CreateContext(mockUpgrade);
        var backendTask = StartBackend(async (stream, ct) =>
        {
            var reader = new WebSocketFrameReader(stream);
            var writer = new WebSocketFrameWriter(stream);

            var frame = await reader.ReadFrame(ct);

            if (frame.Opcode == WebSocketOpcode.Close)
                await writer.WriteClose(WebSocketCloseStatusCode.Normal, ct: ct);
        });

        var proxyTask = Task.Run(() => _proxy.Handle(context), _cts.Token);

        await using var clientConn = new WebSocketConnection(mockUpgrade.TestSideStream);
        await clientConn.CloseAsync(WebSocketCloseStatusCode.Normal, "bye", _cts.Token);

        await backendTask;
        await clientConn.DisposeAsync();
    }

    [Test]
    public async Task HandleWebSocket_backend_sends_data_to_client()
    {
        var mockUpgrade = new MockWebSocketUpgrade();
        var context = CreateContext(mockUpgrade);
        var backendTask = StartBackend(async (stream, ct) =>
        {
            var writer = new WebSocketFrameWriter(stream);
            var reader = new WebSocketFrameReader(stream);

            await writer.WriteText("from-backend", ct);
            await reader.ReadFrame(ct);
        });

        var proxyTask = Task.Run(() => _proxy.Handle(context), _cts.Token);

        await using var clientConn = new WebSocketConnection(mockUpgrade.TestSideStream);
        var msg = await clientConn.ReceiveTextAsync(_cts.Token);
        Assert.That(msg, Is.EqualTo("from-backend"));

        await clientConn.DisposeAsync();
        await backendTask;
    }

    [Test]
    public async Task HandleWebSocket_relays_server_early_data_co_sent_with_handshake()
    {
        var mockUpgrade = new MockWebSocketUpgrade();
        var context = CreateContext(mockUpgrade);

        var backendTask = Task.Run(async () =>
        {
            var client = await _backendListener.AcceptTcpClientAsync(_cts.Token);
            await using var stream = client.GetStream();

            try
            {
                await ReadUpgradeRequest(stream, _cts.Token);

                // Combine the 101 response headers and the first WS text frame
                // into a single write so they arrive in the same TCP segment.
                using var combined = new MemoryStream();
                combined.Write("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: upgrade\r\n\r\n"u8.ToArray());

                var frameWriter = new WebSocketFrameWriter(combined);
                await frameWriter.WriteText("early-data", _cts.Token);

                await stream.WriteAsync(combined.ToArray(), _cts.Token);
                await stream.FlushAsync(_cts.Token);

                var frameReader = new WebSocketFrameReader(stream);
                await frameReader.ReadFrame(_cts.Token);
            }
            finally
            {
                client.Dispose();
            }
        }, _cts.Token);

        var proxyTask = Task.Run(() => _proxy.Handle(context), _cts.Token);

        await using var clientConn = new WebSocketConnection(mockUpgrade.TestSideStream);
        var msg = await clientConn.ReceiveTextAsync(_cts.Token);
        Assert.That(msg, Is.EqualTo("early-data"));

        await clientConn.DisposeAsync();
        await proxyTask;
        await backendTask;
    }
}
