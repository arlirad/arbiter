namespace Arbiter.Core.Interfaces;

public interface IWorker
{
    Task Start();
    Task Stop();
}
