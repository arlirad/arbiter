using System.IO;
using System.Net;
using System.Net.Quic;
using System.Runtime.Versioning;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Middleware;
using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Mappers;
using Arlirad.Http3.Streams;

namespace Arlirad.Http3;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3Transaction(TransactionIdProvider transactionIdProvider, Http3RequestStream requestStream, int port, IPAddress? remoteAddress) : ITransaction
{
    public Protocol Protocol => Protocol.Http3;
    public int Id
    {
        get;
        private set;
    }
    public bool IsSecure => true;
    public int Port => port;
    public IPAddress? RemoteAddress => remoteAddress;

    public Http3RequestStream GetRequestStream() => requestStream;

    public async Task<RequestDto?> GetRequest()
    {
        try
        {
            return await GetRequestCore();
        }
        catch (QuicException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task<RequestDto?> GetRequestCore()
    {
        var headers = new Headers();

        string? method = null, scheme = null, authority = null, path = null, protocol = null;

        foreach (var header in await requestStream.ReadHeaders())
        {
            switch (header.Key)
            {
                case ":method":
                    method = header.Value;
                    break;
                case ":scheme":
                    scheme = header.Value;
                    break;
                case ":authority":
                    authority = header.Value;
                    break;
                case ":path":
                    path = header.Value;
                    break;
                case ":protocol":
                    protocol = header.Value;
                    break;
                default:
                    headers.Add(header.Key, header.Value ?? string.Empty);
                    break;
            }
        }

        if (method is null || scheme is null || authority is null || path is null)
        {
            await EarlyAbort(Status.BadRequest);
            return null;
        }

        if (string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(protocol) || !string.Equals(protocol, "websocket", StringComparison.OrdinalIgnoreCase))
            {
                await EarlyAbort(Status.NotImplemented);
                return null;
            }

            var parsedAuthority = await ParseAuthority(authority, port);

            if (parsedAuthority is null)
                return null;

            Id = transactionIdProvider.Next();

            return new RequestDto {
                TransactionId = Id,
                Method = Method.Get,
                Authority = parsedAuthority,
                Path = path,
                Headers = new ReadOnlyHeaders(headers),
                Upgrade = new H3WebSocketUpgrade(requestStream),
                IsSecure = true,
                RemoteAddress = remoteAddress,
            };
        }

        if (authority.Contains(':'))
        {
            var isIpv6 = authority.StartsWith('[');

            if (isIpv6)
            {
                var closeBracket = authority.IndexOf(']');
                if (closeBracket < 0)
                {
                    await EarlyAbort(Status.BadRequest);
                    return null;
                }

                var portStart = closeBracket + 1;
                if (portStart < authority.Length && authority[portStart] == ':')
                {
                    var portStr = authority[(portStart + 1)..];
                    if (!int.TryParse(portStr, out var authorityPort) || authorityPort != port)
                    {
                        await EarlyAbort(Status.MisdirectedRequest);
                        return null;
                    }
                }

                authority = authority[1..closeBracket];
            }
            else
            {
                var parts = authority.Split(':');

                if (parts.Length > 2)
                {
                    await EarlyAbort(Status.BadRequest);
                    return null;
                }

                if (!int.TryParse(parts[1], out var authorityPort) || authorityPort != port)
                {
                    await EarlyAbort(Status.MisdirectedRequest);
                    return null;
                }

                authority = parts[0];
            }
        }

        var mappedEnum = MethodMapper.ToEnum(method);

        if (!mappedEnum.HasValue)
        {
            await EarlyAbort(Status.BadRequest);
            return null;
        }

        Id = transactionIdProvider.Next();

        return new RequestDto {
            TransactionId = Id,
            Method = mappedEnum.Value,
            Authority = authority,
            Path = path,
            Headers = new ReadOnlyHeaders(headers),
            Stream = await requestStream.ReadFrame(CancellationToken.None) ? requestStream : null,
            IsSecure = true,
            RemoteAddress = remoteAddress,
        };
    }

    public async Task SetResponse(ResponseDto response)
    {
        try
        {
            await WriteStatusAndHeaders((int)response.Status, response.Headers);
        }
        catch (QuicException)
        {
            await AbortCleanup(response);
            return;
        }
        catch (IOException)
        {
            await AbortCleanup(response);
            return;
        }

        await Finish(response);
    }

    private async Task EarlyAbort(Status status)
    {
        try
        {
            await WriteStatusAndHeaders(StatusCodeMapper.ToCode(status));
            await requestStream.FinishAsync();
        }
        finally
        {
            await requestStream.RetireAsync();
        }
    }

    private async Task WriteStatusAndHeaders(int status, ReadOnlyHeaders? responseHeaders = null)
    {
        var headers = new Dictionary<string, List<string>>() {
            [":status"] = [status.ToString()],
        }.AsEnumerable();

        if (responseHeaders is not null)
            headers = headers.Concat(responseHeaders);

        await requestStream.WriteHeaders(headers);
    }

    private async Task Finish(ResponseDto response)
    {
        try
        {
            if (response.Stream is not null && !response.Status.IsBodyForbidden())
            {
                if (response.Stream.CanSeek)
                    await requestStream.CopyFromInSingleFrame(response.Stream);
                else
                    await response.Stream.CopyToAsync(requestStream);

                await response.Stream.FlushAsync();
            }
        }
        catch (QuicException) { }
        catch (IOException) { }
        finally
        {
            if (response.Stream is not null)
                await response.Stream.DisposeAsync();
        }

        try
        {
            await requestStream.FinishAsync();
        }
        catch (QuicException) { }
        catch (IOException) { }

        try
        {
            await requestStream.RetireAsync();
        }
        catch (QuicException) { }
        catch (IOException) { }
    }

    private async Task AbortCleanup(ResponseDto response)
    {
        if (response.Stream is not null)
            await response.Stream.DisposeAsync();

        try
        {
            await requestStream.FinishAsync();
        }
        catch (QuicException) { }
        catch (IOException) { }

        try
        {
            await requestStream.RetireAsync();
        }
        catch (QuicException) { }
        catch (IOException) { }
    }

    private async Task<string?> ParseAuthority(string authority, int expectedPort)
    {
        if (!authority.Contains(':'))
            return authority;

        var isIpv6 = authority.StartsWith('[');

        if (isIpv6)
        {
            var closeBracket = authority.IndexOf(']');
            if (closeBracket < 0)
            {
                await EarlyAbort(Status.BadRequest);
                return null;
            }

            var portStart = closeBracket + 1;
            if (portStart >= authority.Length || authority[portStart] != ':')
                return authority[1..closeBracket];

            var portStr = authority[(portStart + 1)..];
            if (int.TryParse(portStr, out var authorityPort) && authorityPort == expectedPort)
                return authority[1..closeBracket];

            await EarlyAbort(Status.MisdirectedRequest);
            return null;

        }

        var parts = authority.Split(':');

        if (parts.Length > 2)
        {
            await EarlyAbort(Status.BadRequest);
            return null;
        }

        if (int.TryParse(parts[1], out var authPort) && authPort == expectedPort)
            return parts[0];

        await EarlyAbort(Status.MisdirectedRequest);
        return null;
    }
}
