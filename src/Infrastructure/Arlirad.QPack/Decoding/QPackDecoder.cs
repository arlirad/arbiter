using Arlirad.QPack.Common;
using Arlirad.QPack.Models;
using Arlirad.QPack.Streams;

namespace Arlirad.QPack.Decoding;

public class QPackDecoder(Stream encoderIncoming, Stream decoderOutgoing)
{
    private static readonly TimeSpan InsertIncrementCountWaitTime = TimeSpan.FromMilliseconds(100);

    private readonly QPackWriter _decoderOutgoingWriter = new(decoderOutgoing);

    private readonly List<QPackField> _dynamicTable = [];
    private readonly QPackReader _encoderIncomingReader = new(encoderIncoming);
    private readonly List<(long Required, TaskCompletionSource Tcs)> _waiters = [];
    private readonly object _waitersLock = new();
    private long _ackedInsertCount = 0;
    private CancellationTokenSource? _cts;
    private Task? _encoderReadTask;
    private Task? _insertIncrementCountSendTask;

    private TaskCompletionSource _maxTableCapacityTcs = new();

    private long _totalEvictionCount = 0;
    private long _totalInsertCount = 0;

    public long DynamicTableCapacity { get; private set; }
    public long DynamicTableSize { get; private set; }
    public long TotalInsertCount { get => _totalInsertCount; }

    public async ValueTask Start()
    {
        _cts = new CancellationTokenSource();
        _encoderReadTask = EncoderInstructionsRead();
        _dynamicTable.Clear();
    }

    public async Task<QPackFieldSectionReader> GetSectionReader(long streamId, byte[] buffer)
    {
        var stream = new MemoryStream(buffer);
        var reader = new QPackReader(stream);

        var encodedInsertCount = (long)reader.ReadPrefixedInt(8);
        var deltaBase = (long)reader.ReadPrefixedInt(7, out var deltaBaseSign);
        var baseSign = (deltaBaseSign & QPackConsts.DeltaBaseSignMask) == QPackConsts.DeltaBaseSignMask;

        if (encodedInsertCount != 0)
            while (DynamicTableCapacity == 0)
            {
                await _maxTableCapacityTcs.Task;
            }

        var requiredInsertCount = CalculateRequiredInsertCount(encodedInsertCount, DynamicTableCapacity);

        if (!requiredInsertCount.HasValue)
            // TODO: Throw QPACK_ENCODER_STREAM_ERROR if requiredInsertCount is null
            throw new NotImplementedException();

        await WaitForInsertCount(requiredInsertCount.Value);

        var @base = !baseSign
            ? requiredInsertCount.Value + deltaBase
            : requiredInsertCount.Value - deltaBase - 1;

        return new QPackFieldSectionReader(streamId, requiredInsertCount.Value, @base, stream, reader, this);
    }

    public async ValueTask AcknowledgeSection(QPackFieldSectionReader section, CancellationToken ct = default)
    {
        await _decoderOutgoingWriter.WritePrefixedIntAsync(section.StreamId, 7,
            QPackConsts.DecoderInstructionSectionAcknowledgementMask, ct);

        lock (_waitersLock)
        {
            if (section.RequiredInsertCount > _ackedInsertCount)
                _ackedInsertCount = section.RequiredInsertCount;
        }
    }

    public QPackField? GetField(long index, bool isDynamic)
    {
        return isDynamic
            ? _dynamicTable[(int)(index - _totalEvictionCount)]
            : QPackConsts.StaticTable[(int)index];
    }

    private async ValueTask WaitForInsertCount(long required)
    {
        Task task;

        lock (_waitersLock)
        {
            if (_totalInsertCount >= required)
                return;

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((required, tcs));

            task = tcs.Task;
        }

        await task;
    }

    private static int GetEntrySize(string name, string value)
    {
        return name.Length + value.Length + QPackConsts.EntryAdditionalByteCount;
    }

    private long? CalculateRequiredInsertCount(long encodedInsertCount, long dynamicTableCapacity)
    {
        if (encodedInsertCount == 0)
            return 0;

        var maxEntryCount = dynamicTableCapacity / 32;
        var fullRange = 2 * maxEntryCount;

        if (encodedInsertCount > fullRange)
            return null;

        var maxValue = _totalInsertCount + maxEntryCount;
        var maxWrapped = (maxValue / fullRange) * fullRange;
        var reqInsertCount = maxWrapped + encodedInsertCount - 1;

        if (reqInsertCount > maxValue)
        {
            if (reqInsertCount <= fullRange)
                return null;

            reqInsertCount -= fullRange;
        }

        return reqInsertCount != 0
            ? reqInsertCount
            : null;
    }

    private async Task EncoderInstructionsRead()
    {
        var ct = _cts!.Token;
        var buffer = new byte[1];

        while (!_cts.IsCancellationRequested)
        {
            await encoderIncoming.ReadExactlyAsync(buffer, ct);

            var instruction = buffer[0];

            if (QPackConsts.Is(instruction, QPackConsts.EncoderInstructionDynamicTableCapacityMask))
            {
                var capacity =
                    await _encoderIncomingReader.ReadPrefixedIntFromProvidedByteAsync(5, buffer[0], buffer, ct);

                DynamicTableCapacity = (int)capacity;
                _maxTableCapacityTcs.SetResult();
            }
            else if (QPackConsts.Is(instruction, QPackConsts.EncoderInstructionInsertWithNameReferenceMask))
            {
                var index = await _encoderIncomingReader.ReadPrefixedIntFromProvidedByteAsync(5, buffer[0], buffer, ct);
                var value = await _encoderIncomingReader.ReadStringAsync(buffer, ct);

                var isDynamic = (buffer[0] & QPackConsts.EncoderInstructionInsertWithDynamicNameReferenceMask)
                    == QPackConsts.EncoderInstructionInsertWithDynamicNameReferenceMask;

                var indexTransformed = (long)index;

                var referredField = GetField(indexTransformed, isDynamic);

                if (referredField is null)
                {
                    // TODO: Throw QPACK_ENCODER_STREAM_ERROR if referredField is null
                    ;
                    throw new NotImplementedException();
                }

                Insert(referredField.Name, value);
            }
            else if (QPackConsts.Is(instruction, QPackConsts.EncoderInstructionInsertWithLiteralNameMask))
            {
                var name = await _encoderIncomingReader.ReadStringAsync(buffer, 5, instruction, 5, ct);
                var value = await _encoderIncomingReader.ReadStringAsync(buffer, ct);

                Insert(name, value);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    private void Insert(string name, string value)
    {
        var entrySize = GetEntrySize(name, value);

        _dynamicTable.Add(new QPackField(name, value));

        var toRelease = new List<TaskCompletionSource>();

        lock (_waitersLock)
        {
            _totalInsertCount++;

            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                toRelease.Add(_waiters[i].Tcs);
                _waiters.RemoveAt(i);
            }

            if (_insertIncrementCountSendTask is null)
                _insertIncrementCountSendTask = WaitToSendInsertIncrementCount();
        }

        foreach (var taskCompletionSource in toRelease)
        {
            taskCompletionSource.SetResult();
        }

        DynamicTableSize += entrySize;
    }

    private async Task WaitToSendInsertIncrementCount()
    {
        await Task.Delay(InsertIncrementCountWaitTime);

        long increment;

        lock (_waitersLock)
        {
            if (_ackedInsertCount == _totalInsertCount)
                return;

            increment = _totalInsertCount - _ackedInsertCount;
            _insertIncrementCountSendTask = null;
        }

        await _decoderOutgoingWriter.WritePrefixedIntAsync(increment, 6,
            QPackConsts.DecoderInstructionInsertCountIncrementMask, CancellationToken.None);
    }
}