using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Application.Middleware;
using Arbiter.Application.Services;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;

namespace Arbiter.Application.Tests;

public class AltSvcGlobalMiddlewareTests
{
    [Test]
    public async Task Handle_sets_AltSvc_header_when_service_has_value()
    {
        var altSvc = new AltSvcService();
        altSvc.Set("h3", ":443", 86400);
        var context = CreateContext();
        var sut = new AltSvcGlobalMiddleware((_, _, _) => Task.CompletedTask, altSvc);

        await sut.Handle(new TransactionStub(), null, context);

        Assert.That(context.Response.Headers.AltSvc, Is.EqualTo(@"h3="":443""; ma=86400"));
    }

    [Test]
    public async Task Handle_does_not_set_AltSvc_header_when_service_is_empty()
    {
        var altSvc = new AltSvcService();
        var context = CreateContext();
        var sut = new AltSvcGlobalMiddleware((_, _, _) => Task.CompletedTask, altSvc);

        await sut.Handle(new TransactionStub(), null, context);

        Assert.That(context.Response.Headers.AltSvc, Is.Null);
    }

    [Test]
    public async Task Handle_calls_next_delegate()
    {
        var altSvc = new AltSvcService();
        var context = CreateContext();
        var nextInvoked = false;
        var sut = new AltSvcGlobalMiddleware((_, _, _) => {
            nextInvoked = true;
            return Task.CompletedTask;
        }, altSvc);

        await sut.Handle(new TransactionStub(), null, context);

        Assert.That(nextInvoked, Is.True);
    }

    private static Context CreateContext()
    {
        return new ContextFactory().Create(
            1, Method.Get, "/", [], null, null, "localhost", false, null)!;
    }

    private sealed class TransactionStub : ITransaction
    {
        public int Id => 0;
        public Protocol Protocol => Protocol.Http11;
        public bool IsSecure => false;
        public int Port => 80;
        public System.Net.IPAddress? RemoteAddress => null;
        public Task<RequestDto?> GetRequest() => Task.FromResult<RequestDto?>(null);
        public Task SetResponse(ResponseDto response) => Task.CompletedTask;
    }
}
