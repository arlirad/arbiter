using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Domain.ValueObjects;
using Arbiter.Infrastructure.Mappers;
using Arlirad.Http3.Streams;

namespace Arbiter.Transport.Quic;

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
            _ = header.Key switch
            {
                ":method" => method = header.Value,
                ":scheme" => scheme = header.Value,
                ":authority" => authority = header.Value,
                ":path" => path = header.Value,
                _ => headers[header.Key] = header.Value,
            };
        }

        if (method is null || scheme is null || authority is null || path is null)
            return null;

        if (authority.Contains(':'))
        {
            var parts = authority.Split(':');

            if (parts.Length > 2)
                return null;

            if (!int.TryParse(parts[1], out var authorityPort) || authorityPort != port)
                return null;

            authority = parts[0];
        }

        var mappedEnum = MethodMapper.ToEnum(method);

        if (!mappedEnum.HasValue)
            return null;

        return new RequestDto
        {
            Method = mappedEnum.Value,
            Authority = authority,
            Path = path,
            Headers = new ReadOnlyHeaders(headers),
            Stream = requestStream,
        };
    }

    public async Task SetResponse(ResponseDto response)
    {
        var headers = new Dictionary<string, string>()
        {
            [":status"] = ((int)response.Status).ToString(),
        }.AsEnumerable();

        if (response.Stream is not null)
            headers = headers.Concat(new Dictionary<string, string>()
            {
                ["content-length"] = response.Stream.Length.ToString(),
            });

        headers = headers.Concat(response.Headers);

        await requestStream.WriteHeaders(headers);
        _ = Finish(response);
    }

    private async Task Finish(ResponseDto response)
    {
        if (response.Stream is not null)
        {
            await response.Stream.CopyToAsync(requestStream);
            await response.Stream.FlushAsync();
        }

        requestStream.Finish();
    }
}