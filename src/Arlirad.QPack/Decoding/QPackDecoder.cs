using Arlirad.QPack.Common;
using Arlirad.QPack.Models;
using Arlirad.QPack.Streams;

namespace Arlirad.QPack.Decoding;

public class QPackDecoder
{
    private static readonly TimeSpan InsertIncrementCountWaitTime = TimeSpan.FromMilliseconds(100);
    private readonly Task _decoderOutgoingTask;
    private readonly TaskCompletionSource _decoderOutgoingTcs = new();

    private readonly List<QPackField> _dynamicTable = [];
    private readonly TaskCompletionSource _maxTableCapacityTcs = new();
    private readonly List<(long Required, TaskCompletionSource Tcs)> _waiters = [];
    private readonly Lock _waitersLock = new();

    private long _ackedInsertCount;
    private CancellationTokenSource? _cts;
    private Stream? _decoderOutgoing;
    private QPackWriter? _decoderOutgoingWriter;

    private Stream? _encoderIncoming;
    private QPackReader? _encoderIncomingReader;
    private Task? _encoderReadTask;
    private Task? _insertIncrementCountSendTask;
    private bool _started;

    private long _totalEvictionCount;
    private long _totalInsertCount;

    public QPackDecoder()
    {
        _decoderOutgoingTask = _decoderOutgoingTcs.Task;
    }

    public long DynamicTableCapacity { get; private set; }
    public long DynamicTableSize { get; private set; }
    public long TotalInsertCount { get => _totalInsertCount; }

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

    public List<QPackField> GetDynamicTable()
    {
        return [.._dynamicTable];
    }

    public QPackField? GetField(long index, bool isDynamic)
    {
        return isDynamic
            ? _dynamicTable[(int)(index - _totalEvictionCount)]
            : QPackConsts.StaticTable[(int)index];
    }

    private async ValueTask WaitForInsertCount(long required, CancellationToken ct)
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

        await task.WaitAsync(ct);
    }

    private static int GetEntrySize(string name, string? value)
    {
        return name.Length + (value?.Length ?? 0) + QPackConsts.EntryAdditionalByteCount;
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
            await _encoderIncoming!.ReadExactlyAsync(buffer, ct);

            var instruction = buffer[0];

            if (QPackConsts.Is(instruction, 0b1110_0000, QPackConsts.EncoderInstructionDynamicTableCapacity))
            {
                var capacity =
                    await _encoderIncomingReader!.ReadPrefixedIntFromProvidedByteAsync(5, instruction, buffer, ct);

                DynamicTableCapacity = (int)capacity;
                _maxTableCapacityTcs.SetResult();
            }
            else if (QPackConsts.Is(instruction, 0b1000_0000,
                QPackConsts.EncoderInstructionInsertWithNameReference))
            {
                var index = await _encoderIncomingReader!.ReadPrefixedIntFromProvidedByteAsync(6, instruction,
                    buffer,
                    ct);

                var value = await _encoderIncomingReader!.ReadStringAsync(buffer, ct);

                var isDynamic = !QPackConsts.Is(instruction, 0b1100_0000,
                    QPackConsts.EncoderInstructionInsertWithStaticNameReference);

                if (isDynamic)
                    index = FromRelative(index);

                var referredField = GetField((long)index, isDynamic);

                if (referredField is null)
                {
                    // TODO: Throw QPACK_ENCODER_STREAM_ERROR if referredField is null
                    ;
                    throw new NotImplementedException();
                }

                Insert(referredField.Name, value, ct);
            }
            else if (QPackConsts.Is(instruction, 0b1100_0000, QPackConsts.EncoderInstructionInsertWithLiteralName))
            {
                var name = await _encoderIncomingReader!.ReadStringAsync(buffer, 5, instruction, 5, ct);
                var value = await _encoderIncomingReader!.ReadStringAsync(buffer, ct);

                Insert(name, value, ct);
            }
            else if (QPackConsts.Is(instruction, 0b1110_0000, QPackConsts.EncoderInstructionDuplicate))
            {
                var index = await _encoderIncomingReader!.ReadPrefixedIntFromProvidedByteAsync(5, instruction,
                    buffer,
                    ct);

                index = FromRelative(index);

                var referredField = GetField((long)index, true);

                if (referredField is null)
                {
                    // TODO: Throw QPACK_ENCODER_STREAM_ERROR if referredField is null
                    ;
                    throw new NotImplementedException();
                }

                Insert(referredField.Name, referredField.Value, ct);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    private ulong FromRelative(ulong index)
    {
        // We don't really need to worry about overflows, GetField is going to fail anyway if we overflow.
        return (ulong)(_totalInsertCount - (long)index - 1);
    }

    private void Insert(string name, string value, CancellationToken ct)
    {
        var entrySize = GetEntrySize(name, value);

        if (entrySize > DynamicTableCapacity)
            // TODO: QPACK_ENCODER_STREAM_ERROR
            throw new NotImplementedException();

        while (DynamicTableSize + entrySize > DynamicTableCapacity)
        {
            var first = _dynamicTable.First();

            _dynamicTable.Remove(first);
            _totalEvictionCount++;

            DynamicTableSize -= GetEntrySize(first.Name, first.Value);
        }

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

            _insertIncrementCountSendTask ??= WaitToSendInsertIncrementCount(ct);
        }

        foreach (var taskCompletionSource in toRelease)
        {
            taskCompletionSource.SetResult();
        }

        DynamicTableSize += entrySize;
    }

    private async Task WaitToSendInsertIncrementCount(CancellationToken ct)
    {
        try
        {
            await Task.Delay(InsertIncrementCountWaitTime, ct);

            long increment;

            lock (_waitersLock)
            {
                if (_ackedInsertCount == _totalInsertCount)
                    return;

                increment = _totalInsertCount - _ackedInsertCount;
                _ackedInsertCount = _totalInsertCount;
                _insertIncrementCountSendTask = null;
            }

            await _decoderOutgoingTask.WaitAsync(ct);
            await _decoderOutgoingWriter!.WritePrefixedIntAsync(increment, 6,
                QPackConsts.DecoderInstructionInsertCountIncrement, CancellationToken.None);

            await _decoderOutgoing!.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }

    private async Task CancelStream(long streamId, CancellationToken ct)
    {
        await _decoderOutgoingTask.WaitAsync(ct);
        await _decoderOutgoingWriter!.WritePrefixedIntAsync(streamId, 6,
            QPackConsts.DecoderInstructionStreamCancellation, CancellationToken.None);
    }
}