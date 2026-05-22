using Arbiter.Protocol.QPack.Common;
using Arbiter.Protocol.QPack.Models;
using Arbiter.Protocol.QPack.Streams;

namespace Arbiter.Protocol.QPack.Decoding;

public class QPackDecoder
{
    private readonly SemaphoreSlim _capacitySignal = new(0, 1);
    private readonly Task _decoderOutgoingTask;
    private readonly TaskCompletionSource _decoderOutgoingTcs = new();

    private readonly List<QPackField> _dynamicTable = [];

    private readonly ManualResetEvent _encoderInstructionsProcessedEvent = new(false);
    private readonly byte[] _instructionBuffer = new byte[1];
    private readonly List<(long Required, TaskCompletionSource Tcs)> _waiters = [];
    private readonly Lock _waitersLock = new();

    private long _ackedInsertCount;
    private int _capacitySignaled;
    private CancellationTokenSource? _cts;
    private Stream? _decoderOutgoing;
    private QPackWriter? _decoderOutgoingWriter;

    private Stream? _encoderIncoming;
    private QPackReader? _encoderIncomingReader;
    private Task? _encoderReadTask;
    private Task? _insertIncrementCountSendTask;
    private bool _started;

    private long _totalEvictionCount;

    public QPackDecoder()
    {
        _decoderOutgoingTask = _decoderOutgoingTcs.Task;
    }

    public TimeSpan InsertCountIncrementDelay
    {
        get;
        set;
    } = TimeSpan.FromMilliseconds(100);

    public long MaxTableCapacity
    {
        get;
        set;
    }
    public long DynamicTableCapacity
    {
        get;
        private set;
    }
    public long DynamicTableSize
    {
        get;
        private set;
    }
    public long TotalInsertCount
    {
        get;
        private set;
    }

    public ValueTask Start()
    {
        _cts = new CancellationTokenSource();
        _dynamicTable.Clear();

        _started = true;

        return ValueTask.CompletedTask;
    }

    public void SetIncomingStream(Stream stream)
    {
        if (!_started)
            throw new Exception("Attempt to set incoming stream before starting the decoder");

        _encoderIncoming = stream;
        _encoderIncomingReader = new QPackReader(_encoderIncoming);
        _encoderReadTask = EncoderInstructionsRead();
    }

    public void SetOutgoingStream(Stream stream)
    {
        if (!_started)
            throw new Exception("Attempt to set incoming stream before starting the decoder");

        _decoderOutgoing = stream;
        _decoderOutgoingWriter = new QPackWriter(_decoderOutgoing);
        _decoderOutgoingTcs.SetResult();
    }

    public async Task<QPackFieldSectionReader> GetSectionReader(
        long streamId,
        byte[] buffer,
        int length,
        CancellationToken ct = default)
    {
        var stream = new MemoryStream(buffer, 0, length);

        return await GetSectionReader(streamId, stream, ct);
    }

    public async Task<QPackFieldSectionReader> GetSectionReader(long streamId, Stream stream, CancellationToken ct)
    {
        var reader = new QPackReader(stream);

        var encodedInsertCount = (long)reader.ReadPrefixedInt(8);
        var deltaBase = (long)reader.ReadPrefixedInt(7, out var deltaBaseSign);
        var baseSign = (deltaBaseSign & QPackConsts.DeltaBaseSignMask) == QPackConsts.DeltaBaseSignMask;

        if (encodedInsertCount != 0)
        {
            while (DynamicTableCapacity == 0)
            {
                if (_capacitySignaled != 0 && DynamicTableCapacity == 0)
                    throw new InvalidOperationException("QPACK_DECODER_ERROR: Dynamic table capacity not set");

                await _capacitySignal.WaitAsync(ct);
            }
        }

        var requiredInsertCount = CalculateRequiredInsertCount(encodedInsertCount, DynamicTableCapacity);

        if (!requiredInsertCount.HasValue)

            // TODO: Throw QPACK_ENCODER_STREAM_ERROR if requiredInsertCount is null
            throw new NotImplementedException();

        try
        {
            await WaitForInsertCount(requiredInsertCount.Value, ct);
        }
        catch (OperationCanceledException)
        {
            await CancelStream(streamId, ct);
        }

        var @base = !baseSign
            ? requiredInsertCount.Value + deltaBase
            : requiredInsertCount.Value - deltaBase - 1;

        return new QPackFieldSectionReader(streamId, requiredInsertCount.Value, @base, stream, reader, this);
    }

    public async ValueTask AcknowledgeSection(QPackFieldSectionReader section, CancellationToken ct = default)
    {
        await _decoderOutgoingTask.WaitAsync(ct);
        await _decoderOutgoingWriter!.WritePrefixedIntAsync(section.StreamId, 7,
            QPackConsts.DecoderInstructionSectionAcknowledgement, ct);

        lock (_waitersLock)
        {
            if (section.RequiredInsertCount > _ackedInsertCount)
                _ackedInsertCount = section.RequiredInsertCount;
        }
    }

    public List<QPackField> GetDynamicTable() => [.. _dynamicTable];

    public QPackField? GetField(long index, bool isDynamic)
    {
        if (isDynamic)
        {
            var tableIndex = (int)(index - _totalEvictionCount);

            return tableIndex < 0 || tableIndex >= _dynamicTable.Count
                ? throw new InvalidOperationException("QPACK_DECOMPRESSION_FAILED: Invalid dynamic table index")
                : _dynamicTable[tableIndex];
        }

        var staticIndex = (int)index;

        return staticIndex < 0 || staticIndex >= QPackConsts.StaticTable.Count
            ? throw new InvalidOperationException("QPACK_DECOMPRESSION_FAILED: Invalid static table index")
            : QPackConsts.StaticTable[staticIndex];
    }

    private async ValueTask WaitForInsertCount(long required, CancellationToken ct)
    {
        Task task;

        lock (_waitersLock)
        {
            if (TotalInsertCount >= required)
                return;

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((required, tcs));

            task = tcs.Task;
        }

        await task.WaitAsync(ct);
    }

    private static int GetEntrySize(string name, string? value) => name.Length + (value?.Length ?? 0) + QPackConsts.EntryAdditionalByteCount;

    private long? CalculateRequiredInsertCount(long encodedInsertCount, long dynamicTableCapacity)
    {
        if (encodedInsertCount == 0)
            return 0;

        var maxEntryCount = dynamicTableCapacity / 32;
        var fullRange = 2 * maxEntryCount;

        if (encodedInsertCount > fullRange)
            return null;

        var maxValue = TotalInsertCount + maxEntryCount;
        var maxWrapped = maxValue / fullRange * fullRange;
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

        while (!_cts.IsCancellationRequested)
        {
            await _encoderIncoming!.ReadExactlyAsync(_instructionBuffer, ct);

            var instruction = _instructionBuffer[0];

            if (QPackConsts.Is(instruction, 0b1110_0000, QPackConsts.EncoderInstructionDynamicTableCapacity))
            {
                var capacity =
                    await _encoderIncomingReader!.ReadPrefixedIntFromProvidedByteAsync(5, instruction, _instructionBuffer, ct);

                Resize(capacity);
                _capacitySignaled = 1;
                _capacitySignal.Release();
            }
            else if (QPackConsts.Is(instruction, 0b1000_0000,
                QPackConsts.EncoderInstructionInsertWithNameReference))
            {
                var index = await _encoderIncomingReader!.ReadPrefixedIntFromProvidedByteAsync(6, instruction,
                    _instructionBuffer,
                    ct);

                var value = await _encoderIncomingReader!.ReadStringAsync(_instructionBuffer, ct);

                var isDynamic = !QPackConsts.Is(instruction, 0b1100_0000,
                    QPackConsts.EncoderInstructionInsertWithStaticNameReference);

                if (isDynamic)
                    index = FromRelative(index);

                var referredField = GetField((long)index, isDynamic);

                if (referredField is null)
                    throw new InvalidOperationException("QPACK_ENCODER_STREAM_ERROR: invalid table reference");

                Insert(referredField.Name, value, ct);
            }
            else if (QPackConsts.Is(instruction, 0b1100_0000, QPackConsts.EncoderInstructionInsertWithLiteralName))
            {
                var name = await _encoderIncomingReader!.ReadStringAsync(_instructionBuffer, 5, instruction, 5, ct);
                var value = await _encoderIncomingReader!.ReadStringAsync(_instructionBuffer, ct);

                Insert(name, value, ct);
            }
            else if (QPackConsts.Is(instruction, 0b1110_0000, QPackConsts.EncoderInstructionDuplicate))
            {
                var index = await _encoderIncomingReader!.ReadPrefixedIntFromProvidedByteAsync(5, instruction,
                    _instructionBuffer,
                    ct);

                index = FromRelative(index);

                var referredField = GetField((long)index, true);

                if (referredField is null)
                    throw new InvalidOperationException("QPACK_ENCODER_STREAM_ERROR: invalid table reference");

                Insert(referredField.Name, referredField.Value, ct);
            }
            else
            {
                throw new InvalidOperationException($"QPACK_ENCODER_STREAM_ERROR: unknown instruction 0x{instruction:X2}");
            }
        }
    }

    private void Resize(ulong capacity)
    {
        var newCapacity = (long)capacity;
        DynamicTableCapacity = newCapacity;

        while (DynamicTableSize > newCapacity && _dynamicTable.Count > 0)
        {
            var first = _dynamicTable[0];
            _dynamicTable.RemoveAt(0);
            _totalEvictionCount++;
            DynamicTableSize -= GetEntrySize(first.Name, first.Value);
        }
    }

    private ulong FromRelative(ulong index)

        // We don't really need to worry about overflows, GetField is going to fail anyway if we overflow.
        => (ulong)(TotalInsertCount - (long)index - 1);

    private void Insert(string name, string value, CancellationToken ct)
    {
        var entrySize = GetEntrySize(name, value);

        if (entrySize > DynamicTableCapacity)
            throw new InvalidOperationException($"QPACK_ENCODER_STREAM_ERROR: Entry size {entrySize} exceeds dynamic table capacity {DynamicTableCapacity}");

        while (DynamicTableSize + entrySize > DynamicTableCapacity)
        {
            var first = _dynamicTable.First();

            _dynamicTable.Remove(first);
            _totalEvictionCount++;

            DynamicTableSize -= GetEntrySize(first.Name, first.Value);
        }

        _dynamicTable.Add(new QPackField(name, value));

        var toRelease = new List<TaskCompletionSource>();

        bool sendImmediately;

        lock (_waitersLock)
        {
            TotalInsertCount++;

            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                var (required, tcs) = _waiters[i];

                if (required > TotalInsertCount)
                    continue;

                toRelease.Add(tcs);
                _waiters.RemoveAt(i);
            }

            sendImmediately = InsertCountIncrementDelay == TimeSpan.Zero;
            if (!sendImmediately)
                _insertIncrementCountSendTask ??= WaitToSendInsertIncrementCount(ct);
        }

        if (sendImmediately)
            SendInsertCountIncrement(ct);

        foreach (var taskCompletionSource in toRelease)
            taskCompletionSource.SetResult();

        DynamicTableSize += entrySize;
    }

    private void SendInsertCountIncrement(CancellationToken ct)
    {
        if (_ackedInsertCount == TotalInsertCount)
            return;

        var increment = TotalInsertCount - _ackedInsertCount;
        _ackedInsertCount = TotalInsertCount;

        _decoderOutgoingTask.WaitAsync(ct).GetAwaiter().GetResult();
        _decoderOutgoingWriter!.WritePrefixedIntAsync(increment, 6,
            QPackConsts.DecoderInstructionInsertCountIncrement, CancellationToken.None).GetAwaiter().GetResult();

        _decoderOutgoing!.FlushAsync(ct).GetAwaiter().GetResult();
    }

    private async Task WaitToSendInsertIncrementCount(CancellationToken ct)
    {
        try
        {
            if (InsertCountIncrementDelay > TimeSpan.Zero)
                await Task.Delay(InsertCountIncrementDelay, ct);

            long increment;

            lock (_waitersLock)
            {
                if (_ackedInsertCount == TotalInsertCount)
                    return;

                increment = TotalInsertCount - _ackedInsertCount;
                _ackedInsertCount = TotalInsertCount;
                _insertIncrementCountSendTask = null;
            }

            await _decoderOutgoingTask;
            await _decoderOutgoingWriter!.WritePrefixedIntAsync(increment, 6,
                QPackConsts.DecoderInstructionInsertCountIncrement, CancellationToken.None);

            await _decoderOutgoing!.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CancelStream(long streamId, CancellationToken ct)
    {
        await _decoderOutgoingTask.WaitAsync(ct);
        await _decoderOutgoingWriter!.WritePrefixedIntAsync(streamId, 6,
            QPackConsts.DecoderInstructionStreamCancellation, CancellationToken.None);
    }
}
