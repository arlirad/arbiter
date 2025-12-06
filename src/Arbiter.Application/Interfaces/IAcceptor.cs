namespace Arbiter.Application.Interfaces;

public interface IAcceptor
{
    Task<ITransaction> Accept(CancellationToken ct);
}