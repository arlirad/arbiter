using System.Collections;
using Arlirad.QPack.Common;
using Arlirad.QPack.Models;
using Arlirad.QPack.Streams;

namespace Arlirad.QPack.Decoding;

public class QPackFieldSectionReader(
    long streamId,
    long requiredInsertCount,
    long @base,
    Stream stream,
    QPackReader reader,
    QPackDecoder parent
) : IEnumerable<QPackField>, IAsyncDisposable
{
    public long StreamId { get; } = streamId;
    public long RequiredInsertCount { get; } = requiredInsertCount;
    public long Base { get; } = @base;

    public async ValueTask DisposeAsync()
    {
        await parent.AcknowledgeSection(streamId);
        await stream.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    public IEnumerator<QPackField> GetEnumerator()
    {
        while (stream.Position != stream.Length)
        {
            var entry = stream.ReadByte();
            if (entry == -1)
                break;

            if ((entry & QPackConsts.IndexedFieldLineMask) == QPackConsts.IndexedFieldLineMask)
            {
                var index = reader.ReadVarIntFromProvidedByte(6, entry);
                yield return (entry & QPackConsts.IndexedStaticFieldLineMask) == QPackConsts.IndexedStaticFieldLineMask
                    ? QPackConsts.StaticTable[(int)index]
                    : throw new NotImplementedException("Dynamic tables are not yet implemented");
            }
            else if ((entry & QPackConsts.LiteralFieldLineWithNameReferenceMask)
                == QPackConsts.LiteralFieldLineWithNameReferenceMask)
            {
                var index = reader.ReadVarIntFromProvidedByte(4, entry);
                var nameTableEntry = (entry & QPackConsts.LiteralStaticFieldLineWithNameReferenceMask)
                    == QPackConsts.LiteralStaticFieldLineWithNameReferenceMask
                        ? QPackConsts.StaticTable[(int)index]
                        : throw new NotImplementedException("Dynamic tables are not yet implemented");

                var value = reader.ReadString();

                yield return new QPackField(nameTableEntry.Name, value);
            }
            else if ((entry & QPackConsts.IndexedPostBaseFieldLineMask) == QPackConsts.IndexedPostBaseFieldLineMask)
            {
                var index = (long)reader.ReadVarIntFromProvidedByte(4, entry) + Base;

                yield return parent.GetField(index, true)
                    ?? throw new NotImplementedException("QPACK_DECOMPRESSION_FAILED");
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}