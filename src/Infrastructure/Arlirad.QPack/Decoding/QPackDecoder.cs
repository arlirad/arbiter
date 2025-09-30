using Arlirad.QPack.Common;
using Arlirad.QPack.Streams;

namespace Arlirad.QPack.Decoding;

public class QPackDecoder
{
    public async Task<QPackFieldSectionReader> GetSectionReader(long streamId, byte[] buffer)
    {
        var stream = new QPackStream(new MemoryStream(buffer));

        var requiredInsertCount = (int)stream.ReadVarInt(8);
        var deltaBase = (int)stream.ReadVarInt(7, out var deltaBaseSign);
        var baseSign = (deltaBaseSign & QPackConsts.DeltaBaseSignMask) == QPackConsts.DeltaBaseSignMask;

        return new QPackFieldSectionReader(streamId, requiredInsertCount, baseSign, deltaBase, stream);
    }
}