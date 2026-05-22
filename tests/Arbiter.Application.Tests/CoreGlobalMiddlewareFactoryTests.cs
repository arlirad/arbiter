using System.Net;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Middleware;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;

namespace Arbiter.Application.Tests;

public class CoreGlobalMiddlewareFactoryTests
{
    [Test]
    public async Task Create_with_null_site_returns_not_found()
    {
        var sut = new CoreGlobalMiddlewareFactory();
        var @delegate = sut.Create((_, _, _) => Task.CompletedTask);
        var context = CreateContext();

        await @delegate(new DummyTransaction(), null, context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.NotFound));
    }

    [Test]
    public async Task Create_with_site_calls_next()
    {
        var sut = new CoreGlobalMiddlewareFactory();
        var nextCalled = false;
        var @delegate = sut.Create((_, _, _) => {
            nextCalled = true;

            return Task.CompletedTask;
        });

        var context = CreateContext();
        var site = CreateSite();

        await @delegate(new DummyTransaction(), site, context);

        Assert.That(nextCalled, Is.True);
    }

    [Test]
    public async Task Create_catches_exception_from_next()
    {
        var sut = new CoreGlobalMiddlewareFactory();
        var @delegate = sut.Create((_, _, _) => throw new InvalidOperationException("boom"));
        var context = CreateContext();
        var site = CreateSite();

        await @delegate(new DummyTransaction(), site, context);

        Assert.That(context.Response.Status, Is.EqualTo(Status.InternalServerError));
    }

    private static Context CreateContext() => new ContextFactory().Create(1, Method.Get, "/", [], null, null, "localhost", false, null)!;

    private static Site CreateSite() => new("/", [], [], [], _ => Task.CompletedTask);

    private sealed class DummyTransaction : ITransaction
    {
        public int Id => 0;
        public Protocol Protocol => Protocol.Http11;
        public bool IsSecure => false;
        public int Port => 80;
        public IPAddress? RemoteAddress => null;
        public Task<RequestDto?> GetRequest() => Task.FromResult<RequestDto?>(null);
        public Task SetResponse(ResponseDto response) => Task.CompletedTask;
    }
}
