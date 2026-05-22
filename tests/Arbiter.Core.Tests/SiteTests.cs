using Arbiter.Core.Aggregates;
using Arbiter.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Core.Tests;

public class SiteTests
{
    [Test]
    public void Ctor_sets_path_from_constructor()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;

        var bindings = new List<Uri> {
            new("http://localhost:8080"),
        };

        var workers = new List<IWorker>();
        var middlewares = new List<IMiddleware>();

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        Assert.That(site.Path, Is.EqualTo("/test"));
    }

    [Test]
    public void Ctor_sets_bindings_from_constructor()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;

        var bindings = new List<Uri> {
            new("http://localhost:8080"),
            new("https://localhost:8443"),
        };

        var workers = new List<IWorker>();
        var middlewares = new List<IMiddleware>();

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        Assert.That(site.Bindings.Count, Is.EqualTo(2));
        Assert.That(site.Bindings[0].ToString(), Is.EqualTo("http://localhost:8080/"));
        Assert.That(site.Bindings[1].ToString(), Is.EqualTo("https://localhost:8443/"));
    }

    [Test]
    public void Ctor_sets_middleware_from_constructor()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;
        var bindings = new List<Uri>();
        var workers = new List<IWorker>();

        var middlewares = new List<IMiddleware> {
            new StubMiddleware(),
            new StubMiddleware(),
        };

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        Assert.That(site.Middleware.Count, Is.EqualTo(2));
    }

    [Test]
    public void Ctor_sets_workers_from_constructor()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;
        var bindings = new List<Uri>();

        var workers = new List<IWorker> {
            new TrackingWorker(),
            new TrackingWorker(),
        };

        var middlewares = new List<IMiddleware>();

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        Assert.That(site.Workers.Count, Is.EqualTo(2));
    }

    [Test]
    public void Ctor_sets_handle_delegate()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;
        var bindings = new List<Uri>();
        var workers = new List<IWorker>();
        var middlewares = new List<IMiddleware>();

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        Assert.That(site.HandleDelegate, Is.Not.Null);
    }

    [Test]
    public void Data_is_not_null()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;
        var bindings = new List<Uri>();
        var workers = new List<IWorker>();
        var middlewares = new List<IMiddleware>();

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        Assert.That(site.Data, Is.Not.Null);
        Assert.That(site.Data, Is.InstanceOf<ComponentDataContainer>());
    }

    [Test]
    public void Data_returns_same_container_on_each_access()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;
        var bindings = new List<Uri>();
        var workers = new List<IWorker>();
        var middlewares = new List<IMiddleware>();

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        var first = site.Data;
        var second = site.Data;

        Assert.That(ReferenceEquals(first, second), Is.True);
    }

    [Test]
    public void DefaultFiles_is_empty_by_default()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;
        var bindings = new List<Uri>();
        var workers = new List<IWorker>();
        var middlewares = new List<IMiddleware>();

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        Assert.That(site.DefaultFiles, Is.Not.Null);
        Assert.That(site.DefaultFiles.Count, Is.EqualTo(0));
    }

    [Test]
    public void Middleware_returns_read_only_list()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;
        var bindings = new List<Uri>();
        var workers = new List<IWorker>();

        var middlewares = new List<IMiddleware> {
            new StubMiddleware(),
        };

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        Assert.That(site.Middleware, Is.InstanceOf<IReadOnlyList<IMiddleware>>());
    }

    [Test]
    public void Workers_returns_read_only_list()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;
        var bindings = new List<Uri>();

        var workers = new List<IWorker> {
            new TrackingWorker(),
        };

        var middlewares = new List<IMiddleware>();

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        Assert.That(site.Workers, Is.InstanceOf<IReadOnlyList<IWorker>>());
    }

    [Test]
    public async Task Start_calls_start_on_all_workers_in_order()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;
        var bindings = new List<Uri>();
        var worker1 = new TrackingWorker();
        var worker2 = new TrackingWorker();
        var worker3 = new TrackingWorker();

        var workers = new List<IWorker> {
            worker1,
            worker2,
            worker3,
        };

        var middlewares = new List<IMiddleware>();

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        await site.Start();

        Assert.That(worker1.Calls, Is.EqualTo(["start"]));
        Assert.That(worker2.Calls, Is.EqualTo(["start"]));
        Assert.That(worker3.Calls, Is.EqualTo(["start"]));
    }

    [Test]
    public async Task Stop_calls_stop_on_all_workers_in_reverse_order()
    {
        Task HandleDelegate(Context ctx) => Task.CompletedTask;
        var bindings = new List<Uri>();
        var worker1 = new TrackingWorker();
        var worker2 = new TrackingWorker();
        var worker3 = new TrackingWorker();

        var workers = new List<IWorker> {
            worker1,
            worker2,
            worker3,
        };

        var middlewares = new List<IMiddleware>();

        var site = new Site("/test", bindings, middlewares, workers, HandleDelegate);

        await site.Stop();

        Assert.That(worker1.Calls, Is.EqualTo(["stop"]));
        Assert.That(worker2.Calls, Is.EqualTo(["stop"]));
        Assert.That(worker3.Calls, Is.EqualTo(["stop"]));

        worker1.Calls.Clear();
        worker2.Calls.Clear();
        worker3.Calls.Clear();

        await site.Start();
        worker1.Calls.Clear();
        worker2.Calls.Clear();
        worker3.Calls.Clear();

        await site.Stop();

        Assert.That(worker1.Calls[0], Is.EqualTo("stop"));
        Assert.That(worker2.Calls[0], Is.EqualTo("stop"));
        Assert.That(worker3.Calls[0], Is.EqualTo("stop"));
    }

    private class StubMiddleware : IMiddleware
    {
        public Task Configure(string path, ComponentDataContainer data, IConfiguration config) => Task.CompletedTask;
        public Task Handle(Context context) => Task.CompletedTask;
    }

    private class TrackingWorker : IWorker
    {
        public List<string> Calls
        {
            get;
        } = [];
        public int StartIndex
        {
            get;
            set;
        }
        public int StopIndex
        {
            get;
            set;
        }

        public Task Configure(string path, List<Uri> bindings, ComponentDataContainer data, IConfiguration config) => Task.CompletedTask;

        public Task Start()
        {
            Calls.Add("start");

            return Task.CompletedTask;
        }

        public Task Stop()
        {
            Calls.Add("stop");

            return Task.CompletedTask;
        }
    }
}
