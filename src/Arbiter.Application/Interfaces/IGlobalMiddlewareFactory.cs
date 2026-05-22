namespace Arbiter.Application.Interfaces;

public interface IGlobalMiddlewareFactory
{
    HandleDelegate Create(HandleDelegate next);
}
