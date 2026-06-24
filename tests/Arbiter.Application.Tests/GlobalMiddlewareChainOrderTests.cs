using System.Net;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Middleware;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Arbiter.Application.Tests;

public class GlobalMiddlewareChainOrderTests
{
    [Test]
    public async Task Build_executes_factories_in_registration_order()
    {
        var order = new List<string>();

        var services = new ServiceCollection();
        services.AddTransient<IGlobalMiddlewareFactory>(_ => new RecordingFactory("first", order));
        services.AddTransient<IGlobalMiddlewareFactory>(_ => new RecordingFactory("second", order));
        services.AddTransient<IGlobalMiddlewareFactory>(_ => new RecordingFactory("third", order));

        var sp = services.BuildServiceProvider();
        var chain = new GlobalMiddlewareChain();
        chain.Build(sp);

        var context = new ContextFactory().Create(1, Method.Get, "/", [], null, null, "localhost", false, null)!;
        await chain.Delegate(new DummyTransaction(), null, context);

        Assert.That(order, Is.EqualTo(["third", "second", "first"]));
    }

    private sealed class RecordingFactory(string name, List<string> order) : IGlobalMiddlewareFactory
    {
        public GlobalHandleDelegate Create(GlobalHandleDelegate next)
        {
            return async (transaction, site, context) => {
                order.Add(name);
                await next(transaction, site, context);
            };
        }
    }

    private sealed class DummyTransaction : ITransaction
    {
        public int Id => 0;
        public Protocol Protocol => Protocol.Http11;
        public bool IsSecure => false;
        public int Port => 80;
        public IPAddress? RemoteAddress => null;
        public Task<RequestDto?> GetRequest(CancellationToken ct = default) => Task.FromResult<RequestDto?>(null);
        public Task SetResponse(ResponseDto response, CancellationToken ct = default) => Task.CompletedTask;
    }
}
