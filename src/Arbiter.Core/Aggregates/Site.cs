using Arbiter.Core.Interfaces;

namespace Arbiter.Core.Aggregates;

public class Site(
    string path,
    IEnumerable<Uri> bindings,
    IEnumerable<IMiddleware> middlewares,
    IEnumerable<IWorker> workers,
    HandleDelegate handleDelegate)
{
    private readonly List<IMiddleware> _middleware = [.. middlewares];
    private readonly List<IWorker> _workers = [.. workers];

    public string Path
    {
        get;
    } = path;
    public List<Uri> Bindings
    {
        get;
    } = [.. bindings];
    public List<string> DefaultFiles
    {
        get;
    } = [];
    public ComponentDataContainer Data { get; } = new();

    public IReadOnlyList<IMiddleware> Middleware => _middleware.AsReadOnly();
    public IReadOnlyList<IWorker> Workers => _workers.AsReadOnly();

    public HandleDelegate HandleDelegate
    {
        get;
    } = handleDelegate;

    public async Task Start()
    {
        foreach (var worker in _workers)
            await worker.Start();
    }

    public async Task Stop()
    {
        var workersReversed = new List<IWorker>(_workers);
        workersReversed.Reverse();

        foreach (var worker in workersReversed)
            await worker.Stop();
    }
}