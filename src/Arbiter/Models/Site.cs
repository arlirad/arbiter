using Arbiter.Middleware;
using Arbiter.Workers;

namespace Arbiter.Models;

internal class Site(string path, IEnumerable<Uri> bindings, IEnumerable<IMiddleware> middlewares, IEnumerable<IWorker> workers)
{
    public string Path { get; set; } = path;
    public List<Uri> Bindings { get; } = [..bindings];
    public List<string> DefaultFiles { get; } = [];

    public List<IMiddleware> Middlewares { get; } = [..middlewares];
    public List<IWorker> Workers { get; } = [..workers];

    public async Task Start()
    {
        foreach (var worker in Workers)
        {
            await worker.Start();
        }
    }
    
    public async Task Stop()
    {
        var workersReversed = new List<IWorker>(Workers);
        workersReversed.Reverse();
        
        foreach (var worker in workersReversed)
        {
            await worker.Stop();
        }
    }
}