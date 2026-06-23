using System.Buffers;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Infrastructure.Streams;

public static class HeadersFinder
{
    private static readonly byte[] Pattern = "\r\n\r\n"u8.ToArray();

    public static async Task<(Stream? headers, Stream? remainder)> GetHeadersClampedStream(Stream inner, CancellationToken ct = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(16368);

        try
        {
            var offset = 0;

            while (true)
            {
                var length = await inner.ReadAsync(buffer.AsMemory(offset), ct);

                if (length == 0)
                    break;

                offset += length;

                var searchStart = Math.Max(0, offset - length - (Pattern.Length - 1));
                var pattern = Pattern.AsSpan();
                var index = buffer.AsSpan(searchStart, offset - searchStart).IndexOf(pattern);

                if (index != -1)
                {
                    var actualIndex = index + searchStart;
                    var endIndex = actualIndex + pattern.Length;
                    var remainderLength = offset - endIndex;
                    var headersBytes = new byte[endIndex];
                    buffer.AsSpan(0, endIndex).CopyTo(headersBytes);
                    var headers = new MemoryStream(headersBytes);
                    Stream? remainder = null;

                    if (remainderLength > 0)
                    {
                        var remainderBytes = new byte[remainderLength];
                        buffer.AsSpan(endIndex, remainderLength).CopyTo(remainderBytes);
                        remainder = new MemoryStream(remainderBytes);
                    }

                    return (headers, remainder);
                }
            }

            return (null, null);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task<Headers?> ParseHeaders(StreamReader reader, CancellationToken ct = default)
    {
        var headers = new Headers();

        while (true)
        {
            var line = await reader.ReadLineAsync(ct);

            if (line is null)
                return null;

            if (line.Length == 0)
                break;

            var separatorIndex = line.IndexOf(": ", StringComparison.Ordinal);

            if (separatorIndex < 0)
                continue;

            headers.Add(line[..separatorIndex], line[(separatorIndex + 2)..]);
        }

        return headers;
    }
}
