using System.Net;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;

namespace Arbiter.Infrastructure.Headers.Tests;

public class RequestIdGlobalMiddlewareTests
{
    [Test]
    public async Task Handle_adds_XRequestId_header()
    {
        var context = CreateContext();
        var sut = new RequestIdGlobalMiddleware((_, _, _) => Task.CompletedTask);

        await sut.Handle(new TransactionStub(), null, context);

        Assert.That(context.Response.Headers["X-Request-Id"], Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Handle_calls_next_delegate()
    {
        var context = CreateContext();
        var nextInvoked = false;
        var sut = new RequestIdGlobalMiddleware((_, _, _) => {
            nextInvoked = true;

            return Task.CompletedTask;
        });

        await sut.Handle(new TransactionStub(), null, context);

        Assert.That(nextInvoked, Is.True);
    }

    [Test]
    public async Task XRequestId_header_matches_transaction_id()
    {
        var context = CreateContext();
        var transaction = new TransactionStub();
        var sut = new RequestIdGlobalMiddleware((_, _, _) => Task.CompletedTask);

        await sut.Handle(transaction, null, context);

        Assert.That(context.Response.Headers["X-Request-Id"]![0], Is.EqualTo(transaction.Id.ToString()));
    }

    private static Context CreateContext()
    {
        return new ContextFactory().Create(
            1, Method.Get, "/", [], null, null, "localhost", false, null)!;
    }

    private sealed class TransactionStub : ITransaction
    {
        public int Id => 42;
        public Protocol Protocol => Protocol.Http11;
        public bool IsSecure => false;
        public int Port => 80;
        public IPAddress? RemoteAddress => null;
        public Task<RequestDto?> GetRequest(CancellationToken ct = default) => Task.FromResult<RequestDto?>(null);
        public Task SetResponse(ResponseDto response, CancellationToken ct = default) => Task.CompletedTask;
    }
}
