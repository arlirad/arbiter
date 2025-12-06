namespace Arlirad.QPack.Tests.Helpers;

public class RFCHelper
{
    public const long EncoderStream = 0x7FFF_FFFF_FFFF_FFFF;
    public const long DecoderStream = 0x7FFF_FFFF_FFFF_FFFE;

    public static async Task<Dictionary<long, byte[]>> GetRfcExampleBuffers(string input)
    {
        using var reader = new StringReader(input);
        var streams = new Dictionary<long, MemoryStream>();
        MemoryStream? current = null;

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
                break;

            if (line.StartsWith("Stream: "))
            {
                var streamId = line.TrimEnd() switch
                {
                    "Stream: Encoder" => EncoderStream,
                    "Stream: Decoder" => DecoderStream,
                    _ => long.Parse(line[8..]),
                };

                current = new MemoryStream();
                streams[streamId] = current;
            }

            var split = line.Split('|');
            if (split.Length < 2)
                continue;

            var left = split[0]
                .Trim()
                .Replace(" ", "");

            if (string.IsNullOrWhiteSpace(left))
                continue;

            await current!.WriteAsync(StringToByteArray(left));
        }

        return streams
            .Select(kvp => new KeyValuePair<long, byte[]>(kvp.Key, kvp.Value.ToArray()))
            .ToDictionary();
    }

    private static byte[] StringToByteArray(string hex)
    {
        var numberChars = hex.Length;
        var bytes = new byte[numberChars / 2];

        for (var i = 0; i < numberChars; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }

        return bytes;
    }
}