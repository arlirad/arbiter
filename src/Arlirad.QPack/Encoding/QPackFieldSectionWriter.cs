using System.Text;
using Arlirad.QPack.Common;
using Arlirad.QPack.Decoding;
using Arlirad.QPack.Encoding;
using Arlirad.QPack.Streams;

public class QPackFieldSectionWriter(
    long streamId,
    Stream stream,
    QPackWriter writer,
    QPackEncoder parent
) : IAsyncDisposable
{
    private bool _prefixWritten;
    public long StreamId { get; } = streamId;

    public async ValueTask DisposeAsync()
    {
        await stream.FlushAsync();

        GC.SuppressFinalize(this);
    }

    public async Task WritePrefix(CancellationToken ct)
    {
        // TODO: Use proper compression

        await writer.WritePrefixedIntAsync(0, 8, 0b0000_0000, ct);
        await writer.WritePrefixedIntAsync(0, 7, 0b0000_0000, ct);

        _prefixWritten = true;
    }

    public async ValueTask Write(string name, string value, CancellationToken ct)
    {
        if (!_prefixWritten)
            throw new InvalidOperationException("WritePrefix must be called before writing any field sections");

        var staticEntryExact = QPackConsts.StaticTable
            .FirstOrDefault(kvp => kvp.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && kvp.Value.Value == value);

        if (staticEntryExact.Value is not null)
        {
            await writer.WritePrefixedIntAsync((byte)staticEntryExact.Key, 6, QPackConsts.IndexedStaticFieldLine, ct);
            return;
        }

        var staticEntryNameOnly = QPackConsts.StaticTable
            .FirstOrDefault(kvp => kvp.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        var valueBytes = Encoding.UTF8.GetBytes(value);

        if (staticEntryNameOnly.Value is not null)
        {
            await writer.WritePrefixedIntAsync((byte)staticEntryNameOnly.Key, 4,
                QPackConsts.LiteralStaticFieldLineWithNameReference, ct);

            await writer.WritePrefixedIntAsync(valueBytes.Length, 7, 0b0000_0000, ct);
            await stream.WriteAsync(valueBytes, ct);

            return;
        }

        var nameBytes = Encoding.UTF8.GetBytes(name.ToLowerInvariant());

        await writer.WritePrefixedIntAsync(nameBytes.Length, 3, QPackConsts.LiteralFieldLineWithLiteralName, ct);
        await stream.WriteAsync(nameBytes, ct);
        await writer.WritePrefixedIntAsync(valueBytes.Length, 7, 0b0000_0000, ct);
        await stream.WriteAsync(valueBytes, ct);
    }
}