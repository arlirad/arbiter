using System.Collections;
using Arlirad.QPack.Common;
using Arlirad.QPack.Models;
using Arlirad.QPack.Streams;

namespace Arlirad.QPack.Decoding;

public class QPackFieldSectionReader(
    long streamId,
    int requiredInsertCount,
    bool baseSign,
    int deltaBase,
    QPackStream stream
) : IEnumerable<QPackField>
{
    public long StreamId { get; } = streamId;
    public int RequiredInsertCount { get; } = requiredInsertCount;
    public bool BaseSign { get; } = baseSign;
    public int DeltaBase { get; } = deltaBase;

    public IEnumerator<QPackField> GetEnumerator()
    {
        while (stream.Position != stream.Length)
        {
            var entry = stream.ReadByte();
            if (entry == -1)
                break;

            if ((entry & QPackConsts.IndexedFieldLineMask) == QPackConsts.IndexedFieldLineMask)
            {
                var index = stream.ReadVarIntFromProvidedByte(6, entry);
                yield return (entry & QPackConsts.IndexedStaticFieldLineMask) == QPackConsts.IndexedStaticFieldLineMask
                    ? QPackConsts.StaticTable[(int)index]
                    : throw new NotImplementedException("Dynamic tables are not yet implemented");
            }
            else if ((entry & QPackConsts.LiteralFieldLineWithNameReferenceMask)
                == QPackConsts.LiteralFieldLineWithNameReferenceMask)
            {
                var index = stream.ReadVarIntFromProvidedByte(4, entry);
                var nameTableEntry = (entry & QPackConsts.LiteralStaticFieldLineWithNameReferenceMask)
                    == QPackConsts.LiteralStaticFieldLineWithNameReferenceMask
                        ? QPackConsts.StaticTable[(int)index]
                        : throw new NotImplementedException("Dynamic tables are not yet implemented");

                var value = stream.ReadString();

                yield return new QPackField(nameTableEntry.Name, value);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}