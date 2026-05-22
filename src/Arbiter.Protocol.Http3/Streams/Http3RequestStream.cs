using System.Buffers;
using System.Net.Quic;
using System.Runtime.Versioning;
using Arbiter.Protocol.Http3.Enums;
using Arbiter.Protocol.Http3.Framing;
using Arbiter.Protocol.QPack.Decoding;

namespace Arbiter.Protocol.Http3.Streams;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3RequestStream(Http3Connection connection, long streamId, QuicStream inner) : Stream
{
    private readonly Http3FrameReader _reader = new(inner);
    private readonly Http3FrameWriter _writer = new(inner);
    private Http3Frame? _currentDataFrame;
    private QPackFieldSectionReader? _currentHeaderReader;
    private byte[]? _pendingHeaders;
    private int _pendingHeadersLength;
    public bool IsUpgrade
    {
        get;
        private set;
    }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public async Task<List<KeyValuePair<string, string?>>> ReadHeaders(CancellationToken ct = default)
    {
        var headersFrame = await _reader.ReadFrame(ct);
        var length = (int)headersFrame.Stream.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(length);

        QPackFieldSectionReader? reader = null;

        try
        {
            await headersFrame.Stream.ReadExactlyAsync(buffer, 0, length, ct);
            reader = await connection.Decoder.GetSectionReader(streamId, buffer, length, ct);
            _currentHeaderReader = reader;

            var headers = new List<KeyValuePair<string, string?>>();

            foreach (var field in reader)
                headers.Add(new KeyValuePair<string, string?>(field.Name, field.Value));

            return headers;
        }
        finally
        {
            if (reader is not null)
                await reader.DisposeAsync();

            _currentHeaderReader = null;
            ArrayPool<byte>.Shared.Return(buffer, true);
        }
    }

    public async ValueTask WriteHeaders(
        IEnumerable<KeyValuePair<string, List<string>>> headers,
        CancellationToken ct = default)
    {
        const int reserve = 16;
        var buffer = ArrayPool<byte>.Shared.Rent(4096 + reserve);

        try
        {
            using var ms = new MemoryStream(buffer, reserve, buffer.Length - reserve);
            var writer = await connection.Encoder.GetSectionWriter(streamId, ms, ct);

            var flattenedHeaders = headers.SelectMany(h => h.Value.Select(v => (h.Key, v))).ToList();
            await writer.WriteFieldSection(flattenedHeaders, ct);

            var qpackLen = (int)ms.Position;
            var fhLen = 0;
            WriteVarInt((ulong)FrameType.Headers, buffer, ref fhLen);
            WriteVarInt((ulong)qpackLen, buffer, ref fhLen);
            buffer.AsSpan(reserve, qpackLen).CopyTo(buffer.AsSpan(fhLen));

            _pendingHeaders = buffer;
            _pendingHeadersLength = fhLen + qpackLen;
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buffer);

            throw;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Synchronous reads are not supported");

    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => !await ReadFrame(ct) ? 0 : await _currentDataFrame!.Stream.ReadAsync(buffer, ct);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public async override ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_pendingHeaders != null)
        {
            var hdrBuf = _pendingHeaders;
            var hdrLen = _pendingHeadersLength;
            _pendingHeaders = null;

            try
            {
                Span<byte> dfh = stackalloc byte[16];
                var dfhLen = 0;
                WriteVarInt((ulong)FrameType.Data, dfh, ref dfhLen);
                WriteVarInt((ulong)data.Length, dfh, ref dfhLen);

                var totalLen = hdrLen + dfhLen + data.Length;

                if (totalLen <= hdrBuf.Length)
                {
                    dfh[..dfhLen].CopyTo(hdrBuf.AsSpan(hdrLen));
                    data.Span.CopyTo(hdrBuf.AsSpan(hdrLen + dfhLen));
                    await inner.WriteAsync(new ReadOnlyMemory<byte>(hdrBuf, 0, totalLen), ct);
                }
                else
                {
                    dfh[..dfhLen].CopyTo(hdrBuf.AsSpan(hdrLen));
                    await inner.WriteAsync(new ReadOnlyMemory<byte>(hdrBuf, 0, hdrLen + dfhLen), ct);
                    await inner.WriteAsync(data, ct);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(hdrBuf);
            }

            return;
        }

        await _writer.WriteFrameHeader(FrameType.Data, (ulong)data.Length, ct);
        await inner.WriteAsync(data, ct);
    }

    public async Task CopyFromInSingleFrame(Stream stream, CancellationToken ct = default)
    {
        if (_pendingHeaders != null)
        {
            var remaining = (int)(stream.Length - stream.Position);
            var hdrBuf = _pendingHeaders;
            var hdrLen = _pendingHeadersLength;
            _pendingHeaders = null;

            try
            {
                Span<byte> dfh = stackalloc byte[16];
                var dfhLen = 0;
                WriteVarInt((ulong)FrameType.Data, dfh, ref dfhLen);
                WriteVarInt((ulong)remaining, dfh, ref dfhLen);

                var totalLen = hdrLen + dfhLen + remaining;

                if (totalLen <= hdrBuf.Length)
                {
                    dfh[..dfhLen].CopyTo(hdrBuf.AsSpan(hdrLen));
                    await stream.ReadExactlyAsync(hdrBuf.AsMemory(hdrLen + dfhLen, remaining), ct);
                    await inner.WriteAsync(new ReadOnlyMemory<byte>(hdrBuf, 0, totalLen), ct);
                }
                else
                {
                    dfh[..dfhLen].CopyTo(hdrBuf.AsSpan(hdrLen));
                    await inner.WriteAsync(new ReadOnlyMemory<byte>(hdrBuf, 0, hdrLen + dfhLen), ct);
                    await stream.CopyToAsync(inner, ct);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(hdrBuf);
            }

            return;
        }

        await _writer.WriteFrameHeader(FrameType.Data, (ulong)(stream.Length - stream.Position), ct);
        await stream.CopyToAsync(inner, ct);
    }

    public override void Flush()
    {
    }

    public async override Task FlushAsync(CancellationToken ct)
    {
        if (_pendingHeaders != null)
        {
            var buf = _pendingHeaders;
            var len = _pendingHeadersLength;
            _pendingHeaders = null;

            try
            {
                await inner.WriteAsync(new ReadOnlyMemory<byte>(buf, 0, len), ct);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buf);
            }
        }
    }

    public async Task FinishAsync(CancellationToken ct = default)
    {
        if (_pendingHeaders != null)
        {
            var buf = _pendingHeaders;
            var len = _pendingHeadersLength;
            _pendingHeaders = null;

            try
            {
                await inner.WriteAsync(new ReadOnlyMemory<byte>(buf, 0, len), ct);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buf);
            }
        }

        inner.CompleteWrites();
    }

    public async Task RetireAsync(CancellationToken ct = default)
    {
        if (IsUpgrade)
            return;

        var buffer = ArrayPool<byte>.Shared.Rent(4096);

        try
        {
            while (await ReadAsync(buffer.AsMemory(), ct) > 0)
            {
            }
        }
        catch (EndOfStreamException)
        {
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await DisposeAsync();
        }
    }

    public async override ValueTask DisposeAsync()
    {
        if (_pendingHeaders != null)
        {
            ArrayPool<byte>.Shared.Return(_pendingHeaders);
            _pendingHeaders = null;
        }

        await inner.DisposeAsync();
        await base.DisposeAsync();
    }

    public void MarkAsUpgrade() => IsUpgrade = true;

    protected override void Dispose(bool disposing)
    {
        if (_pendingHeaders != null)
        {
            ArrayPool<byte>.Shared.Return(_pendingHeaders);
            _pendingHeaders = null;
        }

        base.Dispose(disposing);
    }

    public async Task<bool> ReadFrame(CancellationToken ct)
    {
        try
        {
            if (_currentDataFrame is null || _currentDataFrame.Stream.Position == _currentDataFrame.Stream.Length)
            {
                _currentDataFrame = await _reader.ReadFrame(ct);

                while (_currentDataFrame.Stream.Length == 0)
                    _currentDataFrame = await _reader.ReadFrame(ct);
            }
        }
        catch (EndOfStreamException)
        {
            return false;
        }

        return true;
    }

    private static void WriteVarInt(ulong value, Span<byte> buffer, ref int offset)
    {
        var length = value switch {
            <= 63 => 1,
            <= 16383 => 2,
            <= 1073741823 => 4,
            _ => 8,
        };

        var prefix = length switch {
            1 => 0b0000_0000,
            2 => 0b0100_0000,
            4 => 0b1000_0000,
            _ => 0b1100_0000,
        };

        var i = offset + length - 1;

        while (i > offset)
        {
            buffer[i--] = (byte)(value & 0xFF);
            value >>= 8;
        }

        buffer[offset] = (byte)(prefix | (byte)(value & 0x3F));
        offset += length;
    }
}
