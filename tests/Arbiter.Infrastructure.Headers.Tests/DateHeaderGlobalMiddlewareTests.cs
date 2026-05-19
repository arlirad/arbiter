using System.Text.RegularExpressions;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;

namespace Arbiter.Infrastructure.Headers.Tests;

public class DateHeaderGlobalMiddlewareTests
{
    [Test]
    public async Task Handle_adds_Date_header()
    {
        var context = CreateContext();
        var sut = new DateHeaderGlobalMiddleware((_, _, _) => Task.CompletedTask);

        await sut.Handle(new TransactionStub(), null, context);

        Assert.That(context.Response.Headers["Date"], Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Handle_calls_next_delegate()
    {
        var context = CreateContext();
        var nextInvoked = false;
        var sut = new DateHeaderGlobalMiddleware((_, _, _) => {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await sut.Handle(new TransactionStub(), null, context);

        Assert.That(nextInvoked, Is.True);
    }

    [Test]
    public async Task Date_header_format_is_IMF_fixdate()
    {
        var context = CreateContext();
        var sut = new DateHeaderGlobalMiddleware((_, _, _) => Task.CompletedTask);

        await sut.Handle(new TransactionStub(), null, context);

        var headerValue = context.Response.Headers["Date"]![0];
        Assert.That(Regex.IsMatch(headerValue, @"^[A-Z][a-z]{2}, \d{2} [A-Z][a-z]{2} \d{4} \d{2}:\d{2}:\d{2} GMT$"), Is.True,
            $"Expected header to match RFC 1123 format but was '{headerValue}'");
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