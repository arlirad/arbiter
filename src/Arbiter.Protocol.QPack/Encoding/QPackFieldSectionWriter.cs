using Arbiter.Protocol.QPack.Common;
using Arbiter.Protocol.QPack.Huffman;
using Arbiter.Protocol.QPack.Streams;

namespace Arbiter.Protocol.QPack.Encoding;

public enum HeaderEncodingDecision
{
    None,
    StaticExact,
    StaticNameRef,
    DynamicExactAcked,
    DynamicExactBlocking,
    DynamicNameRef,
    FullLiteral,
}

public record struct HeaderEncodingPlan(
    string Name,
    string Value,
    HeaderEncodingDecision Decision,
    long? DynamicIndex,
    int? StaticIndex,
    bool ShouldInsert
);

public class QPackFieldSectionWriter(
    long streamId,
    Stream stream,
    QPackWriter writer,
    QPackEncoder parent
) : IAsyncDisposable
{
    private bool _prefixWritten;
    public long StreamId
    {
        get;
    } = streamId;

    public async ValueTask DisposeAsync()
    {
        await stream.FlushAsync();

        GC.SuppressFinalize(this);
    }

    public async Task WritePrefix(CancellationToken ct)
    {
        await writer.WritePrefixedIntAsync(0, 8, 0b0000_0000, ct);
        await writer.WritePrefixedIntAsync(0, 7, 0b0000_0000, ct);

        _prefixWritten = true;
    }

    public async ValueTask Write(string name, string value, CancellationToken ct)
    {
        if (!_prefixWritten)
            throw new InvalidOperationException("WritePrefix must be called before writing any field sections");

        if (QPackConsts.StaticExactIndex.TryGetValue((name, value), out var exactIndex))
        {
            await writer.WritePrefixedIntAsync((byte)exactIndex, 6, QPackConsts.IndexedStaticFieldLine, ct);

            return;
        }

        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
        var huffmanValue = HPackHuffman.Encode(valueBytes);
        var useHuffmanValue = huffmanValue.Length < valueBytes.Length;
        var valueToWrite = useHuffmanValue ? huffmanValue : valueBytes;

        if (QPackConsts.StaticNameIndex.TryGetValue(name, out var nameIndex))
        {
            await writer.WritePrefixedIntAsync((byte)nameIndex, 4,
                QPackConsts.LiteralStaticFieldLineWithNameReference, ct);

            var huffmanBit = (byte)(useHuffmanValue ? QPackConsts.HuffmanStringMask : 0);
            await writer.WritePrefixedIntAsync(valueToWrite.Length, 7, huffmanBit, ct);
            await stream.WriteAsync(valueToWrite, ct);

            return;
        }

        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name.ToLowerInvariant());
        var huffmanName = HPackHuffman.Encode(nameBytes);
        var useHuffmanName = huffmanName.Length < nameBytes.Length;
        var nameToWrite = useHuffmanName ? huffmanName : nameBytes;

        var nameFirstByte = (byte)(QPackConsts.LiteralFieldLineWithLiteralName | (useHuffmanName ? 0x08 : 0));
        await writer.WritePrefixedIntAsync(nameToWrite.Length, 3, nameFirstByte, ct);
        await stream.WriteAsync(nameToWrite, ct);

        var valueHuffmanBit = (byte)(useHuffmanValue ? QPackConsts.HuffmanStringMask : 0);
        await writer.WritePrefixedIntAsync(valueToWrite.Length, 7, valueHuffmanBit, ct);
        await stream.WriteAsync(valueToWrite, ct);
    }

    public async Task WriteFieldSection(IEnumerable<(string name, string value)> headers, CancellationToken ct)
    {
        var headerList = headers.ToList();
        var plans = PlanEncoding(headerList, parent);

        var hasDynamicRefs = plans.Any(p => p.DynamicIndex.HasValue);
        var maxDynamicIndex = hasDynamicRefs
            ? plans.Where(p => p.DynamicIndex.HasValue).Max(p => p.DynamicIndex!.Value)
            : 0;

        var requiredInsertCount = hasDynamicRefs ? maxDynamicIndex + 1 : 0;

        await WritePrefix(requiredInsertCount, requiredInsertCount, ct);

        foreach (var plan in plans)
        {
            if (plan.ShouldInsert)
                await parent.InsertEntry(plan.Name, plan.Value, ct);

            await WriteEncodedField(plan, requiredInsertCount, ct);
        }

        await parent.FlushEncoderStream(ct);
    }

    private static List<HeaderEncodingPlan> PlanEncoding(List<(string name, string value)> headers, QPackEncoder encoder)
    {
        var plans = new List<HeaderEncodingPlan>(headers.Count);

        foreach (var (name, value) in headers)
        {
            HeaderEncodingDecision decision;
            long? dynamicIndex;
            int? staticIndex;
            bool shouldInsert;

            if (QPackConsts.StaticExactIndex.TryGetValue((name, value), out var exactStatic))
            {
                decision = HeaderEncodingDecision.StaticExact;
                dynamicIndex = null;
                staticIndex = exactStatic;
                shouldInsert = false;
            }
            else
            {
                staticIndex = null;

                var dynamicExactIndex = encoder.FindDynamicExact(name, value);

                if (dynamicExactIndex.HasValue)
                {
                    var isAcked = dynamicExactIndex.Value < encoder.AckedInsertCount;

                    if (!isAcked && encoder.CanBlock(encoder.BlockedStreams))
                    {
                        decision = HeaderEncodingDecision.DynamicExactBlocking;
                        dynamicIndex = dynamicExactIndex;
                        shouldInsert = false;
                    }
                    else
                    {
                        decision = HeaderEncodingDecision.DynamicExactAcked;
                        dynamicIndex = dynamicExactIndex;
                        shouldInsert = false;
                    }
                }
                else
                {
                    shouldInsert = encoder.PeerMaxTableCapacity > 0;

                    if (QPackConsts.StaticNameIndex.TryGetValue(name, out var staticName))
                    {
                        decision = HeaderEncodingDecision.StaticNameRef;
                        dynamicIndex = null;
                        staticIndex = staticName;
                    }
                    else
                    {
                        var dynamicNameIndex = encoder.FindDynamicName(name);

                        if (dynamicNameIndex.HasValue)
                        {
                            decision = HeaderEncodingDecision.DynamicNameRef;
                            dynamicIndex = dynamicNameIndex;
                        }
                        else
                        {
                            decision = HeaderEncodingDecision.FullLiteral;
                            dynamicIndex = null;
                        }
                    }
                }
            }

            plans.Add(new HeaderEncodingPlan(name, value, decision, dynamicIndex, staticIndex, shouldInsert));
        }

        return plans;
    }

    private async ValueTask WriteEncodedField(HeaderEncodingPlan plan, long baseIndex, CancellationToken ct)
    {
        switch (plan.Decision)
        {
            case HeaderEncodingDecision.StaticExact:
                await writer.WritePrefixedIntAsync((byte)plan.StaticIndex!.Value, 6, QPackConsts.IndexedStaticFieldLine, ct);

                break;

            case HeaderEncodingDecision.DynamicExactAcked:
            case HeaderEncodingDecision.DynamicExactBlocking:
                var relativeIndex = baseIndex - plan.DynamicIndex!.Value - 1;
                await writer.WritePrefixedIntAsync(relativeIndex, 6, QPackConsts.IndexedDynamicFieldLineMask, ct);

                break;

            case HeaderEncodingDecision.StaticNameRef:
                var valueBytes = System.Text.Encoding.UTF8.GetBytes(plan.Value);
                var huffmanValue = HPackHuffman.Encode(valueBytes);
                var useHuffmanValue = huffmanValue.Length < valueBytes.Length;
                var valueToWrite = useHuffmanValue ? huffmanValue : valueBytes;

                await writer.WritePrefixedIntAsync((byte)plan.StaticIndex!.Value, 4,
                    QPackConsts.LiteralStaticFieldLineWithNameReference, ct);

                var huffmanBit = (byte)(useHuffmanValue ? QPackConsts.HuffmanStringMask : 0);
                await writer.WritePrefixedIntAsync(valueToWrite.Length, 7, huffmanBit, ct);
                await stream.WriteAsync(valueToWrite, ct);

                break;

            case HeaderEncodingDecision.DynamicNameRef:
                var dynValueBytes = System.Text.Encoding.UTF8.GetBytes(plan.Value);
                var dynHuffmanValue = HPackHuffman.Encode(dynValueBytes);
                var dynUseHuffmanValue = dynHuffmanValue.Length < dynValueBytes.Length;
                var dynValueToWrite = dynUseHuffmanValue ? dynHuffmanValue : dynValueBytes;
                var dynRelIndex = baseIndex - plan.DynamicIndex!.Value - 1;

                await writer.WritePrefixedIntAsync(dynRelIndex, 4, QPackConsts.LiteralDynamicFieldLineWithNameReference, ct);

                var dynHuffmanBit = (byte)(dynUseHuffmanValue ? QPackConsts.HuffmanStringMask : 0);
                await writer.WritePrefixedIntAsync(dynValueToWrite.Length, 7, dynHuffmanBit, ct);
                await stream.WriteAsync(dynValueToWrite, ct);

                break;

            case HeaderEncodingDecision.FullLiteral:
                var nameBytes = System.Text.Encoding.UTF8.GetBytes(plan.Name.ToLowerInvariant());
                var huffmanName = HPackHuffman.Encode(nameBytes);
                var useHuffmanName = huffmanName.Length < nameBytes.Length;
                var nameToWrite = useHuffmanName ? huffmanName : nameBytes;

                var fullValueBytes = System.Text.Encoding.UTF8.GetBytes(plan.Value);
                var fullHuffmanValue = HPackHuffman.Encode(fullValueBytes);
                var fullUseHuffmanValue = fullHuffmanValue.Length < fullValueBytes.Length;
                var fullValueToWrite = fullUseHuffmanValue ? fullHuffmanValue : fullValueBytes;

                var nameFirstByte = (byte)(QPackConsts.LiteralFieldLineWithLiteralName | (useHuffmanName ? 0x08 : 0));
                await writer.WritePrefixedIntAsync(nameToWrite.Length, 3, nameFirstByte, ct);
                await stream.WriteAsync(nameToWrite, ct);

                var valueHuffmanBit = (byte)(fullUseHuffmanValue ? QPackConsts.HuffmanStringMask : 0);
                await writer.WritePrefixedIntAsync(fullValueToWrite.Length, 7, valueHuffmanBit, ct);
                await stream.WriteAsync(fullValueToWrite, ct);

                break;

            case HeaderEncodingDecision.None:
            default:
                throw new InvalidOperationException($"Unknown encoding decision: {plan.Decision}");
        }
    }

    private async Task WritePrefix(long requiredInsertCount, long baseIndex, CancellationToken ct)
    {
        long encodedInsertCount;

        if (requiredInsertCount == 0)
        {
            encodedInsertCount = 0;
        }
        else
        {
            var maxEntries = parent.PeerMaxTableCapacity / 32;
            var fullRange = 2L * maxEntries;
            encodedInsertCount = (requiredInsertCount % fullRange) + 1;
        }

        await writer.WritePrefixedIntAsync(encodedInsertCount, 8, 0b0000_0000, ct);

        var deltaBase = baseIndex - requiredInsertCount;
        if (deltaBase < 0)
            await writer.WritePrefixedIntAsync(-deltaBase - 1, 7, 0b1000_0000, ct);
        else
            await writer.WritePrefixedIntAsync(deltaBase, 7, 0b0000_0000, ct);

        _prefixWritten = true;
    }
}
