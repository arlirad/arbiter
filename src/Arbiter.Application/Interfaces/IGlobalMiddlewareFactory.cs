namespace Arbiter.Application.Interfaces;

public interface IGlobalMiddlewareFactory
{
    GlobalHandleDelegate Create(GlobalHandleDelegate next);
}
