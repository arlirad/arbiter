namespace Arbiter.Api;

public interface IApi
{
    Task Run(CancellationToken ct);
}