using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Arbiter.Application.Interfaces;
using Arlirad.Http3;
using Arlirad.Http3.Enums;

namespace Arbiter.Transport.Quic;

#pragma warning disable CA1416

public class QuicAcceptor(ICertificateManager certificateManager) : IAcceptor
{
    private const int Backlog = 128;

    private readonly Dictionary<IPEndPoint, QuicAcceptorListener> _listeners = [];

    private readonly Channel<QuicTransaction> _transactions =
        Channel.CreateBounded<QuicTransaction>(new BoundedChannelOptions(4096));

    public async Task<ITransaction> Accept(CancellationToken ct)
    {
        while (true)
        {
            return await _transactions.Reader.ReadAsync(ct);
        }
    }

    public async Task Bind(IEnumerable<IPEndPoint> endpoints)
    {
        await CreateListeners(endpoints);
        await PruneListeners(endpoints);
    }

    private async Task AcceptLoop(QuicListener listener, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var connection = await listener.AcceptConnectionAsync(ct);
                _ = ConnectionLoop(connection, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }

    private async Task ConnectionLoop(QuicConnection quicConnection, CancellationToken ct)
    {
        try
        {
            await using var connection = new Http3Connection(quicConnection);
            await connection.Start();

            while (true)
            {
                var stream = await connection.GetRequestStream(ct);
                var transaction = new QuicTransaction(stream, quicConnection.LocalEndPoint.Port);

                await _transactions.Writer.WriteAsync(transaction, ct);
            }
        }
        catch (Exception)
        {
            await quicConnection.CloseAsync((long)ErrorCode.InternalError, ct);
        }
    }

    private async Task CreateListeners(IEnumerable<IPEndPoint> endpoints)
    {
        foreach (var endpoint in endpoints.Where(e => !_listeners.ContainsKey(e)))
        {
            var listener = await QuicListener.ListenAsync(new QuicListenerOptions()
            {
                ApplicationProtocols = [SslApplicationProtocol.Http3],
                ListenBacklog = Backlog,
                ListenEndPoint = endpoint,
                ConnectionOptionsCallback = ConnectionOptionsCallback,
            });

            var acceptorSocket = new QuicAcceptorListener(listener);

            _listeners[endpoint] = acceptorSocket;
            _ = AcceptLoop(listener, acceptorSocket.CancellationToken);
        }
    }

    private async Task PruneListeners(IEnumerable<IPEndPoint> endpoints)
    {
        foreach (var endpoint in _listeners.Keys.Where(e => !endpoints.Contains(e)))
        {
            await _listeners[endpoint].Stop();
            await _listeners[endpoint].Close();

            _listeners.Remove(endpoint);
        }
    }

    private ValueTask<QuicServerConnectionOptions> ConnectionOptionsCallback(
        QuicConnection connection,
        SslClientHelloInfo clientHello,
        CancellationToken ct)
    {
        var cert = certificateManager.Get(clientHello.ServerName) ?? certificateManager.GetFallback();
        var options = new QuicServerConnectionOptions
        {
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 1,
            ServerAuthenticationOptions = new SslServerAuthenticationOptions
            {
                ClientCertificateRequired = false,
                ServerCertificate = cert,
                EnabledSslProtocols = SslProtocols.Tls13,
                ApplicationProtocols = [SslApplicationProtocol.Http3],
            },
        };

        return ValueTask.FromResult(options);
    }
}