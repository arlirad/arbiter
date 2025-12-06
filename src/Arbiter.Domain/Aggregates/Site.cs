using Arbiter.Domain.Interfaces;

namespace Arbiter.Domain.Aggregates;

public class Site
{
    private readonly List<IMiddleware> _middleware;
    private readonly List<IWorker> _workers;

    private Dictionary<Type, object> _componentData = new();

    public Site(
        string path,
        IEnumerable<Uri> bindings,
        IEnumerable<IMiddleware> middlewares,
        IEnumerable<IWorker> workers,
        HandleDelegate handleDelegate)
    {
        Path = path;
        Bindings = [.. bindings];
        _middleware = [.. middlewares];
        _workers = [.. workers];
        HandleDelegate = handleDelegate;
    }

    public string Path { get; }
    public List<Uri> Bindings { get; }
    public List<string> DefaultFiles { get; } = [];

    public IReadOnlyList<IMiddleware> Middleware { get => _middleware.AsReadOnly(); }
    public IReadOnlyList<IWorker> Workers { get => _workers.AsReadOnly(); }

    public HandleDelegate HandleDelegate { get; }

    public async Task Start()
    {
        foreach (var worker in _workers)
        {
            await worker.Start();
        }
    }

    public async Task Stop()
    {
        var workersReversed = new List<IWorker>(_workers);
        workersReversed.Reverse();

        foreach (var worker in workersReversed)
        {
            await worker.Stop();
        }
    }

    public T GetComponentData<T>() where T : new()
    {
        return (T)(_componentData.TryGetValue(typeof(T), out var value) ? value : _componentData[typeof(T)] = new T());
    }
}