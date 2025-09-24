using System.Net;
using System.Net.Sockets;

namespace Arbiter;

public class Link
{
    private Request _request;
    private string _uri;
    private Stream _cStream;
    private Stream _sStream;
    private Socket _sSocket;
    private byte[] _cBuffer = new byte[4096];
    private byte[] _sBuffer = new byte[4096];
    private EndPoint _cep;
    private EndPoint _sep;
    private bool _cWriteClosed = false;
    private bool _sWriteClosed = false;
    private object _lock = new object();

    public Link(Request request, string uri)
    {
        _request = request;
        _uri = uri;
        _cStream = request.SocketStream;
        _cep = request.EndPoint;
    }

    public void Begin(IPEndPoint ep)
    {
        _sep = ep;
        _sSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _sSocket.BeginConnect(ep, new AsyncCallback(Connect_Completed), this);
    }

    private void Connect_Completed(IAsyncResult ar)
    {
        try
        {
            _sSocket.EndConnect(ar);
        }
        catch
        {
            try
            {
                using (var writer = new StreamWriter(_cStream, leaveOpen: true))
                {
                    writer.NewLine = "\r\n";

                    writer.WriteLine("HTTP/1.1 404 Not found");
                    writer.WriteLine("");
                    writer.Flush();
                }
            }
            catch { }
            return;
        }

        _sStream = new NetworkStream(_sSocket);

        // _cStream.WriteTimeout = 5000;
        // _sStream.WriteTimeout = 5000;

        try
        {
            Console.WriteLine($"Proxied {_cep} >>> {_sep} ({_request.Method})");

            using (var writer = new StreamWriter(_sStream, leaveOpen: true))
            {
                writer.NewLine = "\r\n";

                writer.WriteLine($"{_request.Method} {_uri} HTTP/1.1");

                foreach (var header in _request.Headers)
                {
                    writer.Write(header.Key);
                    writer.Write(": ");
                    writer.WriteLine(header.Value);
                }

                writer.WriteLine();
                writer.Flush();
            }

            if (_request.Stream != null)
            {
                _request.Stream.CopyToAsync(_sStream).ContinueWith((t) =>
                {
                    _cStream.BeginRead(_cBuffer, 0, _cBuffer.Length, new AsyncCallback(Read_Completed), _cStream);
                });

                _sStream.BeginRead(_sBuffer, 0, _sBuffer.Length, new AsyncCallback(Read_Completed), _sStream);
                return;
            }

            _cStream.BeginRead(_cBuffer, 0, _cBuffer.Length, new AsyncCallback(Read_Completed), _cStream);
            _sStream.BeginRead(_sBuffer, 0, _sBuffer.Length, new AsyncCallback(Read_Completed), _sStream);
        }
        catch { }
    }

    private void Read_Completed(IAsyncResult ar)
    {
        var stream = (Stream)ar.AsyncState;
        var buffer = (stream == _cStream) ? _cBuffer : _sBuffer;
        var opposing = (stream == _cStream) ? _sStream : _cStream;
        int len = 0;

        try
        {
            len = stream.EndRead(ar);
            if (len == 0)
            {
                TearDown();
                return;
            }

            if ((opposing == _cStream) ? !_cWriteClosed : !_sWriteClosed)
                opposing.Write(buffer, 0, len);

            stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(Read_Completed), stream);
        }
        catch
        {
            Console.WriteLine($"Exception on proxy {_cep} >>> {_sep}");
            TearDown();
        }
    }

    private void TearDown()
    {
        Console.WriteLine($"Tearing down proxy {_cep} >>> {_sep}");

        try
        {
            _cStream.Close();
        }
        catch { }
        try
        {
            _cStream.Dispose();
        }
        catch { }

        try
        {
            _sStream.Close();
        }
        catch { }
        try
        {
            _sStream.Dispose();
        }
        catch { }
    }
}