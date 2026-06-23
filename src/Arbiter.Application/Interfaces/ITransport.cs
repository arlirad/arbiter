namespace Arbiter.Application.Interfaces;

public interface ITransport
{
    Task<IConnection> Accept(CancellationToken ct);
}
