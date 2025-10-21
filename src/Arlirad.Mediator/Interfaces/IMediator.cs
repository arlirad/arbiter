namespace Arlirad.Mediator.Interfaces;

public interface IMediator
{
    ValueTask<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken ct)
        where TRequest : IRequest<TResponse>;

    ValueTask Publish<TNotification>(TNotification request, CancellationToken ct) where TNotification : INotification;
    ValueTask FlushHandler(Type handler);
}