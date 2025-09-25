using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Arbiter.Network;

namespace Arbiter;

public class Receiver
{
    public delegate void RequestedHandler(object sender, State state, Request request);
    public event RequestedHandler Requested;

    private const int MAX_CONNECTIONS = 4096 * 16;
    private const int BUFFER_SIZE = 2048;

    ConcurrentStack<SocketAsyncEventArgs> _argsPool = new ConcurrentStack<SocketAsyncEventArgs>();
    Semaphore _connectionSemaphore = new Semaphore(MAX_CONNECTIONS, MAX_CONNECTIONS);

    object _closeLock = new();
    object _sslLock = new();

    public Receiver()
    {
        for (int i = 0; i < MAX_CONNECTIONS; i++)
        {
            var args = new SocketAsyncEventArgs();
            var buffer = new byte[BUFFER_SIZE];

            args.UserToken = new State()
            {
                Buffer = buffer,
                Arguments = args,
            };
            args.Completed += ReceiveEventArgs_Completed;
            args.SetBuffer(buffer, 0, BUFFER_SIZE);

            _argsPool.Push(args);
        }
    }

    public void ReceiveOn(Socket socket)
    {
        Console.WriteLine($"{socket.RemoteEndPoint} opened");

        _connectionSemaphore.WaitOne();
        _argsPool.TryPop(out var args);

        var state = (args.UserToken as State);

        state.Socket = socket;
        state.Offset = 0;
        state.LastCheckOffset = 0;
        state.CopyLeftovers = 0;
        state.EndPoint = socket.RemoteEndPoint;

        Array.Clear(state.Buffer);

        args.SocketFlags = SocketFlags.Peek;

        if (!socket.ReceiveAsync(args))
            ReceiveEventArgs_Completed(socket, args);
    }

    public async Task Reply(State state, Request request, Response response)
    {
        if (response.DontRespond)
        {
            Console.WriteLine($"{state.EndPoint} <%<");
            return;
        }

        Console.WriteLine($"{state.EndPoint} << HTTP/1.1 {response.Code} {response.Phrase}");

        if (request.Version == "HTTP/0.9")
        {
            try
            {
                if (response.Stream != null && !response.SimpleResponse)
                {
                    response.Stream.Position = 0;
                    await response.Stream.CopyToAsync(state.Stream);
                }
                else
                {
                    using (var writer = new StreamWriter(state.Stream, null, -1, true))
                    {
                        writer.Write("HTTP/0.9");
                        writer.Write(' ');
                        writer.Write(response.Code);
                        writer.Write(' ');
                        writer.Write(response.Phrase);
                        writer.WriteLine();
                    }
                }

                state.Socket.Dispose();
            }
            catch { }
            return;
        }

        try
        {
            response.Headers["Connection"] = request.Version == "HTTP/1.0" ? "close" : "keep-alive";

            if (request.Stream != null)
            {
                request.Stream.ClipLeftovers();
                request.Stream.Dispose();
                request.Stream = null;
            }

            if (state.CopyLeftovers > 0)
            {
                Array.Clear(state.Buffer, state.Offset, BUFFER_SIZE - state.Offset);
                Array.Copy(state.Buffer, state.CopyLeftovers, state.Buffer, 0, BUFFER_SIZE - state.Offset);

                state.Offset -= state.CopyLeftovers;
                state.LastCheckOffset = 0;
                state.CopyLeftovers = 0;
            }

            using (var writer = new StreamWriter(state.Stream, null, -1, true))
            {
                writer.NewLine = "\r\n";

                writer.Write("HTTP/1.1");
                writer.Write(' ');
                writer.Write(response.Code);
                writer.Write(' ');
                writer.Write(response.Phrase);
                writer.WriteLine();

                if (response.Stream == null || response.SimpleResponse)
                {
                    writer.WriteLine("Content-Length: 0");
                }
                else
                {
                    writer.Write("Content-Length: ");
                    writer.Write(response.Stream.Length);
                    writer.WriteLine();
                }

                if (response.Mime != null)
                {
                    writer.Write("Content-Type: ");
                    writer.Write(response.Mime);
                    writer.WriteLine();
                }

                foreach (var pair in response.Headers)
                {
                    writer.Write(pair.Key);
                    writer.Write(": ");
                    writer.Write(pair.Value);
                    writer.WriteLine();
                }

                writer.WriteLine();
            }

            if (response.Stream != null && !response.SimpleResponse)
            {
                response.Stream.Position = 0;
                await response.Stream.CopyToAsync(state.Stream);
            }

            if (request.Version == "HTTP/1.0")
            {
                try
                {
                    state.Stream.Flush();
                    state.Socket.Dispose();
                }
                catch { }
            }
            else
            {
                try
                {
                    state.Stream.Flush();
                }
                catch { }
            }
        }
        catch { /*CloseConnection(state.Arguments);*/ }

        Process(state);
    }

    private void ReceiveEventArgs_Completed(object sender, SocketAsyncEventArgs args)
    {
        var state = args.UserToken as State;

        if (args.BytesTransferred == 0 || args.SocketError != SocketError.Success)
        {
            CloseConnection(state);
            return;
        }

        Stream stream = new NetworkStream(state.Socket);

        if (args.Buffer[0] == 22)
        {
            var ssl = new SslStream(stream, false);

            state.Stream = ssl;
            state.Secure = true;

            Array.Clear(state.Buffer);

            try
            {
                ssl.AuthenticateAsServerAsync(new ServerOptionsSelectionCallback(ServerOptionsCallback), null).ContinueWith((Task t) =>
                {
                    try
                    {
                        t.Wait();
                        Process(state);
                    }
                    catch (AggregateException e) { Console.WriteLine(e); }
                    catch { CloseConnection(state); }
                });
            }
            catch { CloseConnection(state); }
            return;
        }

        state.Stream = stream;
        state.Secure = false;

        Array.Clear(state.Buffer);

        try
        {
            Process(state);
        }
        catch { CloseConnection(state); }
    }

    private void Read_Completed(IAsyncResult ar)
    {
        var state = ar.AsyncState as State;
        int len = 0;

        try
        {
            len = state.Stream.EndRead(ar);
            state.Offset += len;
        }
        catch { }

        if (len == 0)
        {
            CloseConnection(state);
            return;
        }

        Process(state);
    }

    private void Authentication_Completed(IAsyncResult ar)
    {
        var state = ar.AsyncState as State;

        try
        {
            (state.Stream as SslStream).EndAuthenticateAsServer(ar);
        }
        catch (AuthenticationException e)
        {
            Console.WriteLine(e);
        }
        catch { /* CloseConnection(state); */ }

        try
        {
            Process(state);
        }
        catch { CloseConnection(state); }
    }

    private void CloseConnection(State state)
    {
        var socket = (state.Arguments.UserToken as State).Socket;
        Console.WriteLine($"{state.EndPoint} closed"); // @ {Environment.StackTrace}

        try
        {
            socket.Shutdown(SocketShutdown.Send);
            socket.Shutdown(SocketShutdown.Receive);
            socket.Shutdown(SocketShutdown.Both);
        }
        catch { }

        _argsPool.Push(state.Arguments);
        _connectionSemaphore.Release();
    }

    private async ValueTask<SslServerAuthenticationOptions> ServerOptionsCallback(SslStream stream, SslClientHelloInfo clientHelloInfo, object? state, CancellationToken cancellationToken)
    {
        var options = new SslServerAuthenticationOptions();

        var path = $"pfx/{clientHelloInfo.ServerName}.pfx";
        var cert = Server.Cache.GetTie<X509Certificate>(path);
        if (cert == null)
        {
            lock (_sslLock)
            {
                cert = Server.Cache.GetTie<X509Certificate>(path);
                if (cert == null)
                {
                    try
                    {
                        if (!File.Exists(path))
                        {
                            Console.WriteLine($"{path} not found!");
                        }
                        else
                        {
                            cert = new X509Certificate2(File.ReadAllBytes(path));
                            Server.Cache.SetTie(path, cert);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }
        }

        options.ServerCertificate = cert;
        options.EncryptionPolicy = EncryptionPolicy.RequireEncryption;
        options.EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;

        return options;
    }

    private int Check(State state)
    {
        int headSpaceCount = 0;

        for (int i = 1; i < state.Offset; i++)
        {
            if (state.Buffer[i] == ' ')
                headSpaceCount++;

            if (state.Buffer[i - 1] == '\r' && state.Buffer[i - 0] == '\n')
            {
                if (headSpaceCount == 1)
                {
                    state.LastCheckOffset = 0;
                    return i + 1;
                }

                break;
            }
        }

        for (int i = Math.Max(state.LastCheckOffset - 5, 3); i < state.Offset; i++)
            if (state.Buffer[i - 3] == '\r' && state.Buffer[i - 2] == '\n' && state.Buffer[i - 1] == '\r' && state.Buffer[i - 0] == '\n')
            {
                state.LastCheckOffset = 0;
                return i + 1;
            }

        state.LastCheckOffset = state.Offset;
        return -1;
    }

    private bool Parse(State state, int len, out Request request)
    {
        string? headline;

        request = new Request();
        request.SocketStream = state.Stream;
        request.EndPoint = state.EndPoint;

        using (var stream = new MemoryStream(state.Buffer, 0, len))
        {
            using (var reader = new StreamReader(stream))
            {
                headline = reader.ReadLine()?.Trim();
                if (headline == null)
                    return false;

                Console.WriteLine($"{state.Socket.RemoteEndPoint} >> {headline}");

                string[] head = headline.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (head.Length < 2)
                    return false;

                request.Method = head[0];
                request.Version = (head.Length < 3 ? "HTTP/0.9" : head[2]);

                if (request.Version != "HTTP/0.9")
                {
                    while (true)
                    {
                        string? line = reader.ReadLine();
                        if (string.IsNullOrEmpty(line))
                            break;

                        int index = line.IndexOf(':');
                        if (index == -1)
                            return false;

                        string key = line.Substring(0, index);
                        string val = line.Substring(index + 2);

                        request.Headers[key.ToLower()] = val;
                    }
                }

                if (head[1][0] != '/')
                    return false;

                string? host;
                string path = head[1];

                path = path.Replace("..", "");

                if (path.IndexOf('?') != -1)
                {
                    var split = path.Split('?');

                    path = split[0];

                    foreach (var entry in split[1].Split('&'))
                    {
                        if (entry.IndexOf('=') == -1)
                            continue;

                        var pair = entry.Split('=');

                        request.Parameters[pair[0]] = Uri.UnescapeDataString(pair[1]);
                    }
                }

                request.Headers.TryGetValue("host", out host);
                host ??= "0.0.0.0:" + (state.Socket.LocalEndPoint as System.Net.IPEndPoint)?.Port;

                string uri = (state.Secure ? "https://" : "http://") + host + path;

                request.Uri = new Uri(uri);
                request.RewrittenUri = new Uri(uri);
            }
        }

        Array.Clear(state.Buffer, state.Offset, BUFFER_SIZE - state.Offset);
        Array.Copy(state.Buffer, len, state.Buffer, 0, BUFFER_SIZE - len);

        state.Offset -= len;
        state.CopyLeftovers = 0;

        if (request.Headers.TryGetValue("content-length", out string? contentLength))
        {
            if (int.TryParse(contentLength, out int dataLen))
            {
                dataLen = Math.Clamp(dataLen, 0, 1024 * 1024 * 128);
                // Console.WriteLine("data len: " + dataLen);

                request.Stream = new ClampedStream(state.Stream, dataLen, state.Buffer, state.Offset);

                state.CopyLeftovers = Math.Clamp(state.Offset, 0, dataLen);
            }
        }

        Requested?.Invoke(this, state, request);
        return true;
    }

    private void Process(State state)
    {
        int requestLen;
        while ((requestLen = Check(state)) != -1)
        {
            if (!Parse(state, requestLen, out var request))
            {
                var response = new Response();
                response.SimpleCode(400);

                Array.Clear(state.Buffer, state.Offset, BUFFER_SIZE - state.Offset);
                Array.Copy(state.Buffer, requestLen, state.Buffer, 0, BUFFER_SIZE - requestLen);

                state.Offset -= requestLen;
                state.CopyLeftovers = 0;

                Reply(state, request, response).ConfigureAwait(false);
            }

            return;
        }

        try
        {
            state.Stream.BeginRead(state.Buffer, state.Offset, 2048 - state.Offset, new AsyncCallback(Read_Completed), state);
        }
        catch { CloseConnection(state); }
    }
}

public class State
{
    public Socket Socket;
    public Stream? Stream;
    public byte[] Buffer;
    public int Offset;
    public int LastCheckOffset;
    public int CopyLeftovers;
    public SocketAsyncEventArgs Arguments;
    public bool Secure;
    public object Lock;
    public EndPoint EndPoint;
}