using System.Threading.Tasks;
using Arbiter.Infrastructure.Network;

internal class Receiver(Handler handler)
{
    public async Task Receive(Session session)
    {
        await session.Receive()
            .ContinueWith((result) => ReceiveComplete(session, result))
            .ConfigureAwait(false);
    }

    private async Task ReceiveComplete(Session session, Task<SessionReceiveResult> task)
    {
        var result = await task;

        if (result.IsClosed || result.IsBad)
            return;

        await handler.Handle(result);

        _ = Receive(session);
    }
}