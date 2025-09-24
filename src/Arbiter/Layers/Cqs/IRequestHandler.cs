namespace Arbiter.Handlers.Cqs;

public interface IRequestHandler<TRequest, TResponse>
{
    public Task<TResponse> Handle(TRequest request, CancellationToken ct);
}