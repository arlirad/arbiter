using System.Buffers;
using System.Net.Quic;
using System.Runtime.Versioning;
using System.Threading.Channels;
using Arbiter.Protocol.Http3.Enums;
using Arbiter.Protocol.Http3.Framing;
using Arbiter.Protocol.Http3.Streams;
using Arbiter.Protocol.QPack.Decoding;
using Arbiter.Protocol.QPack.Encoding;
using Serilog;

namespace Arbiter.Protocol.Http3;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3Connection(QuicConnection connection) : IAsyncDisposable
{
    private const int MaxWaitingStreams = 64;
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "http3");

    private static readonly Dictionary<SettingsParameter, Func<Http3Connection, ulong>> SettingsToWrite = new() {
        [SettingsParameter.QPackMaxTableCapacity] = conn => (ulong)conn.LocalSettings.MaxDecoderDynamicTableCapacity,
        [SettingsParameter.MaxFieldSectionSize] = conn => conn.LocalSettings.MaxFieldSectionSize,
        [SettingsParameter.QPackBlockedStreams] = conn => (ulong)conn.LocalSettings.QPackBlockedStreams,
        [SettingsParameter.EnableConnectProtocol] = _ => 1,
    };

    private readonly CancellationTokenSource _cts = new();
    private readonly Http3ConnectionSettings _peerSettings = new();

    private readonly Channel<Http3RequestStream> _requestStreams =
        Channel.CreateBounded<Http3RequestStream>(MaxWaitingStreams);

    internal readonly QPackDecoder Decoder = new();

    internal readonly QPackEncoder Encoder = new();
    private Stream? _localControlStream;
    private Stream? _peerControlStream;
    public Http3ConnectionSettings LocalSettings
    {
        get;
    } = new() {
        MaxFieldSectionSize = 8192,
        MaxDecoderDynamicTableCapacity = 8192,
    };

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        if (_localControlStream is IAsyncDisposable localStream)
            await localStream.DisposeAsync();

        if (_peerControlStream is IAsyncDisposable peerStream)
            await peerStream.DisposeAsync();

        _cts.Dispose();
    }

    public async Task Start()
    {
        Decoder.MaxTableCapacity = LocalSettings.MaxDecoderDynamicTableCapacity;

        await Encoder.Start();
        await Decoder.Start();

        await OpenOutgoingStreams();
    }

    public Http3RequestStream? FeedInboundStream(QuicStream stream)
    {
        if (stream.Type == QuicStreamType.Unidirectional)
        {
            _ = HandleUnidirectionalStream(stream);

            return null;
        }

        var requestStream = new Http3RequestStream(this, stream.Id, stream);
        _ = _requestStreams.Writer.WriteAsync(requestStream, _cts.Token);

        return requestStream;
    }

    private async Task HandleUnidirectionalStream(QuicStream stream)
    {
        try
        {
            var ct = _cts.Token;

            var buffer = new byte[16];
            var reader = new Http3Reader(stream);
            var type = (StreamType)await reader.ReadVarInt(buffer, ct);

            switch (type)
            {
                case StreamType.Control:
                    if (Interlocked.Exchange(ref _peerControlStream, stream) != null)
                    {
                        await RaiseConnectionError(ErrorCode.StreamCreationError);

                        return;
                    }

                    await HandleControlStream(stream);

                    break;
                case StreamType.Push:
                    await RaiseConnectionError(ErrorCode.StreamCreationError);

                    break;
                case StreamType.Encoder:
                    Encoder.SetIncomingStream(stream);

                    break;
                case StreamType.Decoder:
                    Decoder.SetIncomingStream(stream);

                    break;
            }
        }
        catch (QuicException)
        {
        }
        catch (IOException)
        {
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "HandleUnidirectionalStream error stream {StreamId}", stream.Id);
        }
    }

    private async Task HandleControlStream(QuicStream stream)
    {
        var ct = _cts.Token;

        var varIntBuffer = new byte[8];
        var frameReader = new Http3FrameReader(stream);
        var frame = await frameReader.ReadFrame(ct);

        if (frame.Type != FrameType.Settings)
        {
            await RaiseConnectionError(ErrorCode.MissingSettings);

            return;
        }

        var reader = new Http3Reader(frame.Stream);

        while (frame.Stream.Position < frame.Stream.Length)
            await ReadSetting(reader, varIntBuffer, ct);

        await Encoder.Initialize(_peerSettings.MaxDecoderDynamicTableCapacity, _peerSettings.QPackBlockedStreams);

        while (!ct.IsCancellationRequested)
        {
            frame = await frameReader.ReadFrame(ct);

            switch (frame.Type)
            {
                case FrameType.Settings:
                    await RaiseConnectionError(ErrorCode.SettingsError);

                    return;
                case FrameType.GoAway:
                case FrameType.MaxPushId:
                case FrameType.CancelPush:
                case FrameType.DuplicatePush:
                    await DrainFrame(frame, ct);

                    continue;
                case FrameType.Data:
                case FrameType.Headers:
                    await RaiseConnectionError(ErrorCode.FrameUnexpected);

                    return;
                default:
                    await DrainFrame(frame, ct);

                    continue;
            }
        }
    }

    private static async Task DrainFrame(Http3Frame frame, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);

        try
        {
            while (frame.Stream.Position < frame.Stream.Length)
            {
                var remaining = (int)(frame.Stream.Length - frame.Stream.Position);
                var toRead = Math.Min(remaining, buffer.Length);
                await frame.Stream.ReadExactlyAsync(new Memory<byte>(buffer, 0, toRead), ct);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task ReadSetting(Http3Reader reader, byte[] buffer, CancellationToken ct)
    {
        var setting = (SettingsParameter)await reader.ReadVarInt(buffer, ct);
        var value = await reader.ReadVarInt(buffer, ct);

        switch (setting)
        {
            case SettingsParameter.QPackMaxTableCapacity:
                if (value > int.MaxValue)
                    throw new InvalidOperationException("H3_SETTINGS_ERROR: QPackMaxTableCapacity exceeds maximum");

                _peerSettings.MaxDecoderDynamicTableCapacity = (int)value;

                break;
            case SettingsParameter.MaxFieldSectionSize:
                _peerSettings.MaxFieldSectionSize = (ulong)value;

                break;
            case SettingsParameter.QPackBlockedStreams:
                if (value > int.MaxValue)
                    throw new InvalidOperationException("H3_SETTINGS_ERROR: QPackBlockedStreams exceeds maximum");

                _peerSettings.QPackBlockedStreams = (int)value;

                break;
            case SettingsParameter.EnableConnectProtocol:
                _peerSettings.EnableConnectProtocol = value != 0;

                break;
            case SettingsParameter.H3Datagram:
                break;
            case SettingsParameter.EnableMetadata:
                break;
        }
    }

    private static async ValueTask WriteSetting(
        Http3Writer writer,
        byte[] buffer,
        SettingsParameter type,
        ulong value,
        CancellationToken ct)
    {
        await writer.WriteVarInt((ulong)type, buffer, ct);
        await writer.WriteVarInt(value, buffer, ct);
    }

    private async Task RaiseConnectionError(ErrorCode errorCode)
    {
        await _cts.CancelAsync();
        await connection.CloseAsync((long)errorCode, CancellationToken.None);
    }

    private async Task OpenOutgoingStreams()
    {
        await OpenControlStream();
        await OpenEncoderStream();
        await OpenDecoderStream();
    }

    private async Task<QuicStream> OpenOutgoingStream(StreamType type)
    {
        var ct = _cts.Token;

        var buffer = new byte[16];
        var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, ct);
        var writer = new Http3Writer(stream);

        await writer.WriteVarInt((ulong)type, buffer, ct);

        return stream;
    }

    private async Task OpenControlStream()
    {
        var ct = _cts.Token;
        var varIntBuffer = new byte[8];

        var stream = await OpenOutgoingStream(StreamType.Control);
        var frameWriter = new Http3FrameWriter(stream);

        using (var payload = new MemoryStream())
        {
            var payloadWriter = new Http3Writer(payload);

            foreach (var (parameter, func) in SettingsToWrite)
                await WriteSetting(payloadWriter, varIntBuffer, parameter, func(this), ct);

            payload.Position = 0;
            await frameWriter.WriteFrame(FrameType.Settings, payload, ct);
        }

        _localControlStream = stream;
    }

    private async Task OpenEncoderStream()
    {
        var stream = await OpenOutgoingStream(StreamType.Encoder);
        Encoder.SetOutgoingStream(stream);
    }

    private async Task OpenDecoderStream()
    {
        var stream = await OpenOutgoingStream(StreamType.Decoder);
        Decoder.SetOutgoingStream(stream);
    }
}
