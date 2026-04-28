namespace Arbiter.Application.Interfaces;

public interface IAcceptor
{
    Task<ITransport> Accept(CancellationToken ct);
}
