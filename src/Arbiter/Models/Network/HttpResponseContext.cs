using Arbiter.DTOs;
using Arbiter.Mappers;

namespace Arbiter.Models.Network;

internal class HttpResponseContext(HttpVersion version, Stream stream)
{
    private const string NewLine = "\r\n";

    public HttpVersion Version => version;
    public HttpHeaders Headers { get; } = new();

    public async Task Send(HttpStatusCode statusCode, Stream? responseDataStream = null)
    {
        var statusLineVersion = HttpVersionMapper.ToString(version);
        var statusLineCode = HttpStatusCodeMapper.ToCode(statusCode);
        var statusLineReason = HttpStatusCodeMapper.ToReasonPhrase(statusCode);
        var statusLine = $"{statusLineVersion} {statusLineCode} {statusLineReason}";

        await using var writer = new StreamWriter(stream, leaveOpen: true);

        writer.NewLine = NewLine;

        await writer.WriteLineAsync(statusLine);

        if (responseDataStream is not null)
            Headers["Content-Length"] = responseDataStream.Length.ToString();

        foreach (var header in Headers)
        {
            await writer.WriteLineAsync($"{header.Key}: {header.Value}");
        }

        await writer.WriteLineAsync();
        await writer.FlushAsync();

        if (responseDataStream is not null)
            await responseDataStream.CopyToAsync(stream);
    }
}