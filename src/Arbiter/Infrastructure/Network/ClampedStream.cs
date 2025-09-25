using System;

namespace Arbiter.Infrastructure.Network;

public class ClampedStream : Stream
{
    private Stream _base;
    private int _remaining;
    private byte[] _buffer;
    private int _bufferOffset;
    private int _bufferLength;
    private int _length;

    public override bool CanRead { get => _base.CanRead; }
    public override bool CanWrite { get => false; }
    public override bool CanSeek { get => false; }

    public override long Length { get => throw new NotSupportedException(); }
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public long Remaining { get => _remaining; }

    public ClampedStream(Stream stream, int limit, byte[] buffer, int bufferLen) : base()
    {
        _base = stream;
        _remaining = limit;
        _buffer = buffer;
        _bufferOffset = 0;
        _bufferLength = bufferLen;
    }

    public void ClipLeftovers()
    {
        int rem_len = Math.Clamp(_remaining, 0, _bufferLength - _bufferOffset);
        _remaining -= rem_len;

        if (_remaining == 0)
            return;

        Console.WriteLine("clipping " + _remaining);

        byte[] buffer = new byte[1024];

        while (_remaining > 0)
        {
            int len = Read(buffer, 0, Math.Clamp(_remaining, 0, 1024));
            if (len == 0)
                break;

            _remaining -= len;
        }
    }

    public override void SetLength(long len)
        => _base.SetLength(len);

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bufferRead = 0;

        if (count > _remaining)
        {
            count = _remaining;

            if (count == 0)
                return count;
        }

        if (_buffer != null && count > 0)
        {
            int len = Math.Clamp(count, 0, _bufferLength - _bufferOffset);
            Array.Copy(_buffer, _bufferOffset, buffer, offset, len);

            offset += len;
            _bufferOffset += len;
            count -= len;
            bufferRead = len;

            if (_bufferOffset == _bufferLength)
                _buffer = null;

            if (count == 0)
            {
                _remaining -= len;
                return bufferRead;
            }
        }

        _remaining -= bufferRead;

        while (count > 0)
        {
            int ret = _base.Read(buffer, offset, count);
            if (ret == 0)
                break;

            _remaining -= ret;
            count -= ret;
            offset += ret;
            bufferRead += ret;
        }

        // Console.WriteLine(ret + " but wanted " + (count + bufferRead) + " remaining "+ _remaining);

        return bufferRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override void Flush()
        => _base.Flush();

    public override long Seek(long val, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }
}