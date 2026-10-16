using System.Net;
using System.Net.Sockets;
using System.Text;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Proxy;
using Arbiter.Infrastructure.Proxy.Config;
using Serilog;

namespace Arbiter.Infrastructure.Proxy.Tests;

[TestFixture]
public class ProxyHostForwardingTests
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
        await _proxy.Configure(new ComponentDataContainer(), new ProxyConfig { Target = new Uri($"http://localhost:{_backendPort}") });
    }

    [TearDown]
    public void TearDown()
    {
        _cts.Cancel();
        _cts.Dispose();
        _backendListener.Stop();
    }

    private static Context CreateContext(string authority, string path)
    {
        // Mirrors the real pipeline: the HTTP/1.1 parser moves Host into Authority and strips it from the
        // headers, and HTTP/3 only provides the :authority pseudo-header, so no Host header exists here.
        return _contextFactory.Create(
            1,
            Method.Get,
            path,
            new Dictionary<string, List<string>>(),
            null,
            null,
            authority,
            false,
            null
        )!;
    }

    private static async Task<string> ReadRequestHead(Stream stream, CancellationToken ct)
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
                break;
        }

        return Encoding.ASCII.GetString(buffer, 0, totalRead);
    }

    private static List<string> HeaderValues(string requestHead, string name)
        => [.. requestHead.Split("\r\n")
            .Where(line => line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[(name.Length + 1)..].Trim())];

    [Test]
    public async Task Forwards_original_authority_as_host_header()
    {
        const string authority = "opencode.example.com:8443";
        var context = CreateContext(authority, "/api/session");

        var backendTask = Task.Run(async () => {
            var client = await _backendListener.AcceptTcpClientAsync(_cts.Token);
            await using var stream = client.GetStream();

            try
            {
                var requestHead = await ReadRequestHead(stream, _cts.Token);
                var response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nContent-Type: text/plain\r\n\r\nok"u8.ToArray();
                await stream.WriteAsync(response, _cts.Token);
                await stream.FlushAsync(_cts.Token);

                return requestHead;
            }
            finally
            {
                client.Dispose();
            }
        }, _cts.Token);

        await _proxy.Handle(context);
        var requestHead = await backendTask;

        Assert.Multiple(() => {
            Assert.That(context.Response.Status, Is.EqualTo(Status.Ok));

            // Exactly one Host header carrying the client's authority verbatim, not the upstream target's host.
            Assert.That(HeaderValues(requestHead, "Host"), Is.EqualTo(new List<string> { authority }));
            Assert.That(HeaderValues(requestHead, "X-Forwarded-Host"), Is.EqualTo(new List<string> { authority }));
        });
    }
}
