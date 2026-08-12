using Arbiter.Application.Interfaces;
using Arbiter.Protocol.Http3.Tests.Fakes;

namespace Arbiter.Protocol.Http3.Tests;

public class FakeMultiplexedConnectionTests
{
    [Test]
    public async Task OpenStreamAsync_returns_stream_with_requested_direction()
    {
        await using var conn = new FakeMultiplexedConnection();

        var uni = await conn.OpenStreamAsync(MultiplexedStreamDirection.Unidirectional);
        var bi = await conn.OpenStreamAsync(MultiplexedStreamDirection.Bidirectional);

        Assert.That(uni.Direction, Is.EqualTo(MultiplexedStreamDirection.Unidirectional));
        Assert.That(bi.Direction, Is.EqualTo(MultiplexedStreamDirection.Bidirectional));
        Assert.That(uni.StreamId, Is.Not.EqualTo(bi.StreamId));
    }

    [Test]
    public async Task GetStreams_yields_enqueued_inbound_streams()
    {
        await using var conn = new FakeMultiplexedConnection();
        conn.EnqueueInbound(new FakeMultiplexedStream(42, MultiplexedStreamDirection.Bidirectional));

        using var cts = new CancellationTokenSource();
        var first = await StreamFirstAsync(conn, cts.Token);

        Assert.That(first, Is.Not.Null);
        Assert.That(first!.StreamId, Is.EqualTo(42));
        cts.Cancel();
    }

    private static async Task<ITransportStream?> StreamFirstAsync(IMultiplexedConnection conn, CancellationToken ct)
    {
        await foreach (var s in conn.GetStreams(ct))
            return s;
        return null;
    }
}
