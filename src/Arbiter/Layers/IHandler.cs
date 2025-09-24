namespace Arbiter.Handlers;

public interface IHandler
{
    public void Configure(object config);
    public Task<bool> CanHandle(Request request);
    public Task<bool> Handle(Request request, Response response);
}