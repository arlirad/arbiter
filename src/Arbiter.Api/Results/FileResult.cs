using System.Text.Json;
using Arbiter.Api.Http;

namespace Arbiter.Api.Results;

public class FileContentResult(byte[] content, string contentType, string? fileName = null) : IActionResult
{
    private readonly byte[] _content = content;
    private readonly string _contentType = contentType;
    private readonly string? _fileName = fileName;

    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = _contentType;

        if (_fileName is not null)
            context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{_fileName}\"";

        context.Response.Headers["Content-Length"] = _content.Length.ToString();

        await context.Response.WriteAsync(_content);
    }
}

public class FileStreamResult(Func<Stream> streamFactory, string contentType, string? fileName = null) : IActionResult
{
    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = contentType;

        if (fileName is not null)
            context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

        await using var stream = streamFactory();

        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            await context.Response.WriteAsync([.. buffer.Take(bytesRead)]);
    }
}