namespace Arlirad.QPack;

public class QPackDecoder
{
    public async Task<QPackFieldSection> GetSection(byte[] buffer)
    {
        var stream = new QPackStream(new MemoryStream(buffer));

        var requiredInsertCount = (int)stream.ReadVarInt(8);
        var deltaBase = (int)stream.ReadVarInt(7, out var deltaBaseSign);
        var baseSign = (deltaBaseSign & QPackConsts.DeltaBaseSignMask) == QPackConsts.DeltaBaseSignMask;

        return new QPackFieldSection(requiredInsertCount, baseSign, deltaBase, stream);
    }
}