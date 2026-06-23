using System.Net;
using System.Text.RegularExpressions;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;

namespace Arbiter.Infrastructure.Headers.Tests;

public class ServerHeaderGlobalMiddlewareTests
{
    [Test]
    public async Task Handle_adds_Server_header()
    {
        var context = CreateContext();
        var sut = new ServerHeaderGlobalMiddleware((_, _, _) => Task.CompletedTask);

        await sut.Handle(new TransactionStub(), null, context);

        Assert.That(context.Response.Headers["Server"], Is.Not.Null.And.Not.Empty);
        Assert.That(context.Response.Headers["Server"]![0], Does.StartWith("Arbiter/"));
    }

    [Test]
    public async Task Handle_calls_next_delegate()
    {
        var context = CreateContext();
        var nextInvoked = false;
        var sut = new ServerHeaderGlobalMiddleware((_, _, _) => {
            nextInvoked = true;

            return Task.CompletedTask;
        });

        await sut.Handle(new TransactionStub(), null, context);

        Assert.That(nextInvoked, Is.True);
    }

    [Test]
    public async Task Server_header_format_is_name_slash_version()
    {
        var context = CreateContext();
        var sut = new ServerHeaderGlobalMiddleware((_, _, _) => Task.CompletedTask);

        await sut.Handle(new TransactionStub(), null, context);

        var headerValue = context.Response.Headers["Server"]![0];
        Assert.That(Regex.IsMatch(headerValue, @"^Arbiter/\d+\.\d+\.\d+(-[\w.]+)?$"), Is.True,
            $"Expected header to match 'Arbiter/X.Y.Z[-suffix]' but was '{headerValue}'");
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
        public IPAddress? RemoteAddress => null;
        public Task<RequestDto?> GetRequest(CancellationToken ct = default) => Task.FromResult<RequestDto?>(null);
        public Task SetResponse(ResponseDto response, CancellationToken ct = default) => Task.CompletedTask;
    }
}
