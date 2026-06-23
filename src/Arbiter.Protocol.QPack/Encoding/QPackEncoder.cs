using Arbiter.Protocol.QPack.Common;
using Arbiter.Protocol.QPack.Huffman;
using Arbiter.Protocol.QPack.Models;
using Arbiter.Protocol.QPack.Streams;

namespace Arbiter.Protocol.QPack.Encoding;

public class QPackEncoder
{
    private readonly HashSet<long> _blockedStreams = [];

    private readonly List<QPackField> _dynamicTable = [];
    private readonly byte[] _instructionBuffer = new byte[8];
    private readonly Lock _tableLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _blockedStreamCount;
    private CancellationTokenSource? _cts;
    private Stream? _decoderIncoming;
    private QPackReader? _decoderIncomingReader;
    private Task? _decoderReadTask;
    private long _dynamicTableCapacity;
    private long _dynamicTableSize;
    private Stream? _encoderOutgoing;
    private QPackWriter? _encoderOutgoingWriter;
    private long _totalEvictionCount;

    public int PeerMaxTableCapacity
    {
        get;
        private set;
    }

    public int BlockedStreams
    {
        get;
        private set;
    }

    public long TotalInsertCount
    {
        get;
        private set;
    }
    public long AckedInsertCount
    {
        get;
        private set;
    }

    public ValueTask Start()
    {
        _cts = new CancellationTokenSource();

        return ValueTask.CompletedTask;
    }

    public async Task Initialize(int maxTableCapacity, int blockedStreams)
    {
        lock (_tableLock)
        {
            _dynamicTableCapacity = maxTableCapacity;
            _dynamicTableSize = 0;
            TotalInsertCount = 0;
            _totalEvictionCount = 0;
            AckedInsertCount = 0;
            _blockedStreamCount = 0;
            _blockedStreams.Clear();
            PeerMaxTableCapacity = maxTableCapacity;
            BlockedStreams = blockedStreams;
        }

        if (_encoderOutgoingWriter is not null)
        {
            await _writeLock.WaitAsync();

            try
            {
                await _encoderOutgoingWriter.WritePrefixedIntAsync(maxTableCapacity, 5,
                    QPackConsts.EncoderInstructionDynamicTableCapacity, CancellationToken.None);
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }

    public bool CanBlock(int limit) => _blockedStreamCount < limit;

    public void SetIncomingStream(Stream stream)
    {
        if (_cts is null)
            throw new Exception("Encoder not started");

        _decoderIncoming = stream;
        _decoderIncomingReader = new QPackReader(_decoderIncoming);
        _decoderReadTask = DecoderInstructionsRead(_cts.Token);
    }

    public void SetOutgoingStream(Stream stream)
    {
        _encoderOutgoing = stream;
        _encoderOutgoingWriter = new QPackWriter(_encoderOutgoing);
    }

    public Task<QPackFieldSectionWriter> GetSectionWriter(
        long streamId,
        Stream stream,
        CancellationToken ct = default) => Task.FromResult(new QPackFieldSectionWriter(streamId, stream, new QPackWriter(stream), this));

    public async Task FlushEncoderStream(CancellationToken ct)
    {
        if (_encoderOutgoing is null)
            return;

        await _writeLock.WaitAsync(ct);

        try
        {
            await _encoderOutgoing.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static int GetEntrySize(string name, string? value) => name.Length + (value?.Length ?? 0) + 32;

    private void EvictFor(int entrySize)
    {
        lock (_tableLock)
        {
            while (_dynamicTableSize + entrySize > _dynamicTableCapacity && _dynamicTable.Count > 0)
            {
                var first = _dynamicTable[0];
                _dynamicTable.RemoveAt(0);
                _totalEvictionCount++;
                _dynamicTableSize -= GetEntrySize(first.Name, first.Value);
            }
        }
    }

    private long ToRelativeIndex(long absoluteIndex) => TotalInsertCount - absoluteIndex - 1;

    public List<QPackField> GetDynamicTable()
    {
        lock (_tableLock)
        {
            return [.. _dynamicTable];
        }
    }

    public long? FindDynamicExact(string name, string value)
    {
        lock (_tableLock)
        {
            for (var i = _dynamicTable.Count - 1; i >= 0; i--)
            {
                if (_dynamicTable[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase) && _dynamicTable[i].Value == value)
                    return TotalInsertCount - _dynamicTable.Count + i;
            }

            return null;
        }
    }

    public long? FindDynamicName(string name)
    {
        lock (_tableLock)
        {
            for (var i = _dynamicTable.Count - 1; i >= 0; i--)
            {
                if (_dynamicTable[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return TotalInsertCount - _dynamicTable.Count + i;
            }

            return null;
        }
    }

    private async Task SendInsertWithStaticNameRef(int staticIndex, byte[] valueBytes, CancellationToken ct)
    {
        await _encoderOutgoingWriter!.WritePrefixedIntAsync(staticIndex, 6, 0b1100_0000, ct);
        await WriteStringValue(valueBytes, ct);
    }

    private async Task SendInsertWithDynamicNameRef(long relativeIndex, byte[] valueBytes, CancellationToken ct)
    {
        await _encoderOutgoingWriter!.WritePrefixedIntAsync(relativeIndex, 6, 0b1000_0000, ct);
        await WriteStringValue(valueBytes, ct);
    }

    private async Task SendInsertWithLiteralName(byte[] nameBytes, byte[] valueBytes, CancellationToken ct)
    {
        await _encoderOutgoingWriter!.WritePrefixedIntAsync(nameBytes.Length, 5, 0b0100_0000, ct);
        await _encoderOutgoing!.WriteAsync(nameBytes, ct);
        await WriteStringValue(valueBytes, ct);
    }

    private async Task SendDuplicate(long relativeIndex, CancellationToken ct) => await _encoderOutgoingWriter!.WritePrefixedIntAsync(relativeIndex, 5, 0b0000_0000, ct);

    private async Task WriteStringValue(byte[] valueBytes, CancellationToken ct)
    {
        var huffmanValue = HPackHuffman.Encode(valueBytes);
        var useHuffman = huffmanValue.Length < valueBytes.Length;
        var valueToWrite = useHuffman ? huffmanValue : valueBytes;
        var huffmanBit = (byte)(useHuffman ? 0b1000_0000 : 0);

        await _encoderOutgoingWriter!.WritePrefixedIntAsync(valueToWrite.Length, 7, huffmanBit, ct);
        await _encoderOutgoing!.WriteAsync(valueToWrite, ct);
    }

    public async Task<long> InsertEntry(string name, string value, CancellationToken ct)
    {
        var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
        var entrySize = GetEntrySize(name, value);

        await _writeLock.WaitAsync(ct);

        try
        {
            if (QPackConsts.StaticNameIndex.TryGetValue(name, out var staticIndex))
            {
                await SendInsertWithStaticNameRef(staticIndex, valueBytes, ct);
            }
            else
            {
                var dynamicNameIndex = FindDynamicName(name);

                if (dynamicNameIndex is not null)
                {
                    var relativeIndex = ToRelativeIndex(dynamicNameIndex.Value);
                    await SendInsertWithDynamicNameRef(relativeIndex, valueBytes, ct);
                }
                else
                {
                    var nameBytes = System.Text.Encoding.UTF8.GetBytes(name.ToLowerInvariant());
                    await SendInsertWithLiteralName(nameBytes, valueBytes, ct);
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }

        EvictFor(entrySize);

        lock (_tableLock)
        {
            _dynamicTable.Add(new QPackField(name, value));
            _dynamicTableSize += entrySize;
            TotalInsertCount++;
        }

        return TotalInsertCount - 1;
    }

    private async Task DecoderInstructionsRead(CancellationToken ct)
    {
        if (_decoderIncoming is null || _decoderIncomingReader is null)
            return;

        while (!ct.IsCancellationRequested)
        {
            await _decoderIncoming.ReadExactlyAsync(_instructionBuffer, 0, 1, ct);

            var firstByte = _instructionBuffer[0];

            if (QPackConsts.Is(firstByte, 0b0000_0000, 0b0000_0000))
            {
                var increment = await _decoderIncomingReader.ReadPrefixedIntFromProvidedByteAsync(6, firstByte, _instructionBuffer, ct);

                lock (_tableLock)
                {
                    AckedInsertCount += (long)increment;
                }
            }
            else if (QPackConsts.Is(firstByte, 0b1000_0000, 0b1000_0000))
            {
                var streamIdValue = await _decoderIncomingReader.ReadPrefixedIntFromProvidedByteAsync(7, firstByte, _instructionBuffer, ct);

                lock (_tableLock)
                {
                    _blockedStreams.Remove((long)streamIdValue);
                    _blockedStreamCount--;
                }
            }
            else if (QPackConsts.Is(firstByte, 0b0100_0000, 0b0100_0000))
            {
                var streamIdValue = await _decoderIncomingReader.ReadPrefixedIntFromProvidedByteAsync(6, firstByte, _instructionBuffer, ct);

                lock (_tableLock)
                {
                    _blockedStreams.Remove((long)streamIdValue);
                    _blockedStreamCount--;
                }
            }
        }
    }
}
