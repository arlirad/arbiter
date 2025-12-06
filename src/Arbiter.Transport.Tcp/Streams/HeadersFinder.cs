using Arbiter.Infrastructure.Streams;

namespace Arbiter.Transport.Tcp.Streams;

public static class HeadersFinder
{
    private static readonly byte[] Pattern = "\r\n\r\n"u8.ToArray();

    public static async Task<(Stream? headers, Stream? remainder)> GetHeadersClampedStream(Stream inner)
    {
        var buffer = new byte[16368];
        var offset = 0;

        while (true)
        {
            var length = await inner.ReadAsync(buffer.AsMemory(offset));
            var searchStart = Math.Max(0, offset - (Pattern.Length - 1));
            var pattern = Pattern.AsSpan();
            var index = buffer.AsSpan(searchStart).IndexOf(pattern);

            offset += length;

            if (index != -1)
            {
                var actualIndex = index + searchStart;
                var endIndex = actualIndex + pattern.Length;
                var remainderLength = offset - endIndex;
                var headers = new MemoryStream(buffer, 0, endIndex);
                var remainder = remainderLength > 0
                    ? new MemoryStream(buffer, endIndex, remainderLength)
                    : null;

                return (headers, remainder);
            }

            if (length == 0)
                break;
        }

        return (null, null);
    }
}