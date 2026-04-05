namespace Arbiter.Infrastructure.Streams;

public static class StreamRelay
{
    public static async Task BidirectionalCopy(Stream streamA, Stream streamB, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var aToB = streamA.CopyToAsync(streamB, cts.Token);
        var bToA = streamB.CopyToAsync(streamA, cts.Token);

        await Task.WhenAny(aToB, bToA);
        await cts.CancelAsync();

        try
        {
            await Task.WhenAll(aToB, bToA);
        }
        catch (OperationCanceledException)
        {
        }
    }
}