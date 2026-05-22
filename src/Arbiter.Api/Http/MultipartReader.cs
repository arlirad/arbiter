using System.Text;
using System.Text.RegularExpressions;

namespace Arbiter.Api.Http;

public partial class MultipartReader(string boundary, Stream stream)
{
    private readonly string _boundary = boundary;
    private readonly Stream _stream = stream;

    public async Task<IFormFile?> ReadNextFileAsync()
    {
        if (_stream.CanSeek)
            _stream.Position = 0;

        using var reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
        var content = await reader.ReadToEndAsync();

        var parts = content.Split($"--{_boundary}", StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part) || part.Trim() == "--")
                continue;

            var headerEnd = part.IndexOf("\r\n\r\n");

            if (headerEnd < 0)
                continue;

            var headers = part[..headerEnd];
            var body = part[(headerEnd + 4)..];

            var nameMatch = MyRegex().Match(headers);
            var fileNameMatch = MyRegex1().Match(headers);
            var contentTypeMatch = Regex.Match(headers, @"Content-Type:\s*([^\r\n]+)");

            if (fileNameMatch.Success)
            {
                var name = nameMatch.Success ? nameMatch.Groups[1].Value : "file";
                var fileName = fileNameMatch.Groups[1].Value;
                var contentType = contentTypeMatch.Success ? contentTypeMatch.Groups[1].Value : "application/octet-stream";

                body = body.TrimStart('\r', '\n').TrimEnd('\r', '\n');

                var bytes = Encoding.UTF8.GetBytes(body);
                var stream = new MemoryStream(bytes);

                return new FormFile(stream, contentType, fileName, name);
            }
        }

        return null;
    }

    public static List<IFormFile> Parse(Stream stream, string contentType)
    {
        var files = new List<IFormFile>();

        if (!contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return files;

        var boundaryMatch = Regex.Match(contentType, @"boundary=(.+)$");

        if (!boundaryMatch.Success)
            return files;

        _ = boundaryMatch.Groups[1].Value.Trim('"');

        return files;
    }

    [GeneratedRegex(@"name=""([^""]+)""")]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"filename=""([^""]+)""")]
    private static partial Regex MyRegex1();
}
