using System.Collections;
using Arlirad.Infrastructure.QPack.Common;
using Arlirad.Infrastructure.QPack.Models;
using Arlirad.Infrastructure.QPack.Streams;

namespace Arlirad.Infrastructure.QPack.Decoding;

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
        if (RequiredInsertCount != 0)
            await parent.AcknowledgeSection(this);

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

            if (QPackConsts.Is(entry, 0b1100_0000, QPackConsts.IndexedStaticFieldLine))
            {
                var index = (long)reader.ReadPrefixedIntFromProvidedByte(6, entry);
                yield return parent.GetField(index, false)
                    ?? throw new NotImplementedException("QPACK_DECOMPRESSION_FAILED");
            }
            else if (QPackConsts.Is(entry, 0b1100_0000, QPackConsts.IndexedDynamicFieldLineMask))
            {
                var index = (long)reader.ReadPrefixedIntFromProvidedByte(6, entry);
                yield return parent.GetField(index, true)
                    ?? throw new NotImplementedException("QPACK_DECOMPRESSION_FAILED");
            }
            else if (QPackConsts.Is(entry, 0b1111_0000, QPackConsts.IndexedFieldLinePostBaseIndex))
            {
                var index = (long)reader.ReadPrefixedIntFromProvidedByte(4, entry) + Base;

                yield return parent.GetField(index, true)
                    ?? throw new NotImplementedException("QPACK_DECOMPRESSION_FAILED");
            }
            else if (QPackConsts.Is(entry, 0b1100_0000, QPackConsts.LiteralFieldLineWithNameReference))
            {
                var index = reader.ReadPrefixedIntFromProvidedByte(4, entry);
                var nameTableEntry = (entry & QPackConsts.LiteralStaticFieldLineWithNameReference)
                    == QPackConsts.LiteralStaticFieldLineWithNameReference
                        ? QPackConsts.StaticTable[(int)index]
                        : throw new NotImplementedException("Dynamic tables are not yet implemented");

                var value = reader.ReadString();

                yield return new QPackField(nameTableEntry.Name, value);
            }
            else if (QPackConsts.Is(entry, 0b1111_0000, QPackConsts.LiteralFieldLineWithPostBaseNameReference))
            {
                var index = (long)reader.ReadPrefixedIntFromProvidedByte(3, entry) + Base;
                var field = parent.GetField(index, true)
                    ?? throw new NotImplementedException("QPACK_DECOMPRESSION_FAILED");

                var value = reader.ReadString();

                yield return new QPackField(field.Name, value);
            }
            else if (QPackConsts.Is(entry, 0b1110_0000, QPackConsts.LiteralFieldLineWithLiteralName))
            {
                var name = reader.ReadString(prefix: 3, entry, huffmanBit: 3);
                var value = reader.ReadString();

                yield return new QPackField(name, value);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}