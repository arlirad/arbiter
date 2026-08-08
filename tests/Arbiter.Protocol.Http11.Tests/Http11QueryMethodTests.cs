using System.Net;
using System.Net.Sockets;
using System.Text;
using Arbiter.Core.Enums;
using Arbiter.Infrastructure.Middleware;
using Arbiter.Protocol.Http11;

namespace Arbiter.Protocol.Http11.Tests;

[TestFixture]
public class Http11QueryMethodTests
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
    public async Task GetRequest_parses_query_method_with_body()
    {
        var (serverStream, clientStream) = CreateStreamPair();

        await using (serverStream)
        await using (clientStream)
        {
            var queryRequest = "QUERY /q HTTP/1.1\r\nHost: localhost\r\nContent-Length: 5\r\n\r\nhello"u8.ToArray();
            await clientStream.WriteAsync(queryRequest, _cts.Token);
            await clientStream.FlushAsync(_cts.Token);

            var transaction = new Http11Transaction(new TransactionIdProvider(), serverStream, false, 80, IPAddress.Loopback);
            var request = await transaction.GetRequest();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(request, Is.Not.Null);
                Assert.That(request!.Method, Is.EqualTo(Method.Query));
                Assert.That(request.Path, Is.EqualTo("/q"));
            }

            var body = new byte[16];
            var read = await request!.Stream!.ReadAsync(body, _cts.Token);

            Assert.That(Encoding.UTF8.GetString(body, 0, read), Is.EqualTo("hello"));
        }
    }
}
