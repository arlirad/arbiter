using System.Runtime.Versioning;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Domain.Enums;
using Arbiter.Domain.ValueObjects;
using Arbiter.Infrastructure.Mappers;
using Arlirad.Http3.Streams;

namespace Arbiter.Transport.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class QuicTransaction(Http3RequestStream requestStream, int port) : ITransaction
{
    public bool IsSecure { get => true; }
    public int Port { get => port; }

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

        var mappedEnum = MethodMapper.ToEnum(method);

        if (!mappedEnum.HasValue)
        {
            await EarlyAbort(Status.BadRequest);
            return null;
        }

        return new RequestDto
        {
            Method = mappedEnum.Value,
            Authority = authority,
            Path = path,
            Headers = new ReadOnlyHeaders(headers),
            Stream = await requestStream.ReadFrame(CancellationToken.None) ? requestStream : null,
        };
    }

    public async Task SetResponse(ResponseDto response)
    {
        await WriteStatusAndHeaders((int)response.Status, response.Headers);
        _ = Finish(response);
    }

    private async Task EarlyAbort(Status status)
    {
        await WriteStatusAndHeaders(StatusCodeMapper.ToCode(status));
        requestStream.Finish();
    }

    private async Task WriteStatusAndHeaders(int status, ReadOnlyHeaders? responseHeaders = null)
    {
        var headers = new Dictionary<string, List<string>>()
        {
            [":status"] = [status.ToString()],
        }.AsEnumerable();

        if (responseHeaders is not null)
            headers = headers.Concat(responseHeaders);

        await requestStream.WriteHeaders(headers);
    }

    private async Task Finish(ResponseDto response)
    {
        if (response.Stream is not null)
        {
            if (response.Stream.CanSeek)
                await requestStream.CopyFromInSingleFrame(response.Stream);
            else
                await response.Stream.CopyToAsync(requestStream);

            await response.Stream.FlushAsync();
        }

        requestStream.Finish();
    }
}