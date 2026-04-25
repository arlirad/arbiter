using System.Net;
using System.Runtime.Versioning;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Enums;
using Arbiter.Core.ValueObjects;
using Arbiter.Infrastructure.Mappers;
using Arlirad.Http3.Streams;

namespace Arlirad.Http3;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3Transaction(Http3RequestStream requestStream, int port, IPAddress? remoteAddress) : ITransaction
{
    private static int _nextId;

    public string Protocol => "h3";
    public int Id
    {
        get;
    } = Interlocked.Increment(ref _nextId);
    public bool IsSecure => true;
    public int Port => port;
    public IPAddress? RemoteAddress => remoteAddress;

    public async Task<RequestDto?> GetRequest()
    {
        var headers = new Headers();

        string? method, scheme, authority, path = authority = scheme = method = null;

        await foreach (var header in requestStream.ReadHeaders())
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

        var mappedEnum = Arbiter.Infrastructure.Mappers.MethodMapper.ToEnum(method);

        if (!mappedEnum.HasValue)
        {
            await EarlyAbort(Status.BadRequest);
            return null;
        }

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
        await WriteStatusAndHeaders((int)response.Status, response.Headers);
        await Finish(response);
    }

    private async Task EarlyAbort(Status status)
    {
        await WriteStatusAndHeaders(StatusCodeMapper.ToCode(status));
        requestStream.Finish();
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
        finally
        {
            if (response.Stream is not null)
                await response.Stream.DisposeAsync();

            requestStream.Finish();
        }
    }
}