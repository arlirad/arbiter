using System.Buffers;
using Arbiter.Api.Http;

namespace Arbiter.Api.Results;

public class FileContentResult(byte[] content, string contentType, string? fileName = null) : IActionResult
{
    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = contentType;

        if (fileName is not null)
            context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

        context.Response.Headers["Content-Length"] = content.Length.ToString();

        await context.Response.WriteAsync(content);
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

        var buffer = ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                await context.Response.WriteAsync([.. buffer.Take(bytesRead)]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
