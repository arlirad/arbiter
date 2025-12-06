namespace Arbiter.Application.Interfaces;

public interface IServer
{
    Task Run(CancellationToken ct);
}