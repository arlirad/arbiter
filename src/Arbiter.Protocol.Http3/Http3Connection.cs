using System.Net.Quic;
using System.Runtime.Versioning;
using System.Threading.Channels;
using Arlirad.Http3.Enums;
using Arlirad.Http3.Framing;
using Arlirad.Http3.Streams;
using Arlirad.Infrastructure.QPack.Decoding;
using Arlirad.Infrastructure.QPack.Encoding;

namespace Arlirad.Http3;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("windows")]
public class Http3Connection(QuicConnection connection) : IAsyncDisposable
{
    private const int MaxWaitingStreams = 64;

    private static readonly Dictionary<SettingsParameter, Func<Http3Connection, ulong>> SettingsToWrite = new()
    {
        [SettingsParameter.QPackMaxTableCapacity] = (conn) => (ulong)conn.LocalSettings.MaxDecoderDynamicTableCapacity,
    };

    private readonly CancellationTokenSource _cts = new();
    private readonly Http3ConnectionSettings _peerSettings = new();

    private readonly Channel<Http3RequestStream> _requestStreams =
        Channel.CreateBounded<Http3RequestStream>(MaxWaitingStreams);

    internal readonly QPackDecoder Decoder = new();

    internal readonly QPackEncoder Encoder = new();
    private Stream? _localControlStream;
    private Stream? _peerControlStream;
    public Http3ConnectionSettings LocalSettings { get; } = new()
    {
        MaxFieldSectionSize = 8192,
        MaxDecoderDynamicTableCapacity = 8192,
    };

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        _cts.Dispose();
    }

    public async Task Start()
    {
        Decoder.MaxTableCapacity = LocalSettings.MaxDecoderDynamicTableCapacity;

        await Encoder.Start();
        await Decoder.Start();

        _ = AcceptIncomingStreams();
        await OpenOutgoingStreams();
    }

    public async Task<Http3RequestStream> GetRequestStream(CancellationToken ct)
    {
        return await _requestStreams.Reader.ReadAsync(ct);
    }

    private async Task AcceptIncomingStreams()
    {
        var ct = _cts.Token;

        while (!ct.IsCancellationRequested)
        {
            var stream = await connection.AcceptInboundStreamAsync(ct);

            _ = stream.Type == QuicStreamType.Unidirectional
                ? HandleUnidirectionalStream(stream)
                : HandleBidirectionalStream(stream);
        }
    }

    private async Task HandleBidirectionalStream(QuicStream stream)
    {
        var ct = _cts.Token;
        var requestStream = new Http3RequestStream(this, stream.Id, stream);

        await _requestStreams.Writer.WriteAsync(requestStream, ct);
    }

    private async Task HandleUnidirectionalStream(QuicStream stream)
    {
        var ct = _cts.Token;
        var buffer = new byte[16];

        var reader = new Http3Reader(stream);
        var type = (StreamType)await reader.ReadVarInt(buffer, ct);

        switch (type)
        {
            case StreamType.Control:
                if (_peerControlStream is not null)
                {
                    await RaiseConnectionError(ErrorCode.StreamCreationError);
                    return;
                }

                _peerControlStream = stream;
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
            default:
                break;
        }
    }

    private async Task HandleControlStream(QuicStream stream)
    {
        var ct = _cts.Token;

        var frameReader = new Http3FrameReader(stream);
        var frame = await frameReader.ReadFrame(ct);

        if (frame.Type != FrameType.Settings)
        {
            await RaiseConnectionError(ErrorCode.MissingSettings);
            return;
        }

        var reader = new Http3Reader(frame.Stream);

        while (frame.Stream.Position < frame.Stream.Length)
        {
            await ReadSetting(reader, ct);
        }

        await frameReader.ReadFrame(ct);
        await RaiseConnectionError(ErrorCode.FrameUnexpected);

        await Task.Delay(-1, ct);
    }

    private async Task ReadSetting(Http3Reader reader, CancellationToken ct)
    {
        var buffer = new byte[8];
        var setting = (SettingsParameter)await reader.ReadVarInt(buffer, ct);
        var value = await reader.ReadVarInt(buffer, ct);

        switch (setting)
        {
            case SettingsParameter.QPackMaxTableCapacity:
                _peerSettings.MaxDecoderDynamicTableCapacity = (int)value;
                break;
            case SettingsParameter.MaxFieldSectionSize:
                _peerSettings.MaxFieldSectionSize = (int)value;
                break;
            case SettingsParameter.QPackBlockedStreams:
                break;
            case SettingsParameter.EnableConnectProtocol:
                break;
            case SettingsParameter.H3Datagram:
                break;
            case SettingsParameter.EnableMetadata:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(setting), setting, null);
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

    private async Task RaiseConnectionError(ErrorCode streamCreationError)
    {
        await _cts.CancelAsync();
        await connection.CloseAsync((long)streamCreationError, CancellationToken.None);
    }

    private async Task OpenOutgoingStreams()
    {
        await Task.WhenAll(OpenControlStream(), OpenEncoderStream(), OpenDecoderStream());
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

        var buffer = new byte[8];
        var stream = await OpenOutgoingStream(StreamType.Control);
        var frameWriter = new Http3FrameWriter(stream);

        using (var payload = new MemoryStream())
        {
            var payloadWriter = new Http3Writer(payload);

            foreach (var (parameter, func) in SettingsToWrite)
            {
                await WriteSetting(payloadWriter, buffer, parameter, func(this), ct);
            }

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