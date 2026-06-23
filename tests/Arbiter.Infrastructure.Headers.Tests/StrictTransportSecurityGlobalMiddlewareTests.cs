using System.Net;
using Arbiter.Application.Configuration;
using Arbiter.Application.DTOs;
using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;

namespace Arbiter.Infrastructure.Headers.Tests;

public class StrictTransportSecurityGlobalMiddlewareTests
{
    [Test]
    public async Task Handle_adds_StrictTransportSecurity_header_on_secure_connection()
    {
        var context = CreateContext();
        var sut = new StrictTransportSecurityGlobalMiddleware((_, _, _) => Task.CompletedTask, CreateConfig());

        await sut.Handle(new TransactionStub(true), null, context);

        Assert.That(context.Response.Headers["Strict-Transport-Security"], Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Handle_does_not_add_header_on_insecure_connection()
    {
        var context = CreateContext();
        var sut = new StrictTransportSecurityGlobalMiddleware((_, _, _) => Task.CompletedTask, CreateConfig());

        await sut.Handle(new TransactionStub(), null, context);

        Assert.That(context.Response.Headers["Strict-Transport-Security"], Is.Null.Or.Empty);
    }

    [Test]
    public async Task Handle_calls_next_delegate()
    {
        var context = CreateContext();
        var nextInvoked = false;

        var sut = new StrictTransportSecurityGlobalMiddleware((_, _, _) => {
            nextInvoked = true;

            return Task.CompletedTask;
        }, CreateConfig());

        await sut.Handle(new TransactionStub(true), null, context);

        Assert.That(nextInvoked, Is.True);
    }

    [Test]
    public async Task Header_contains_max_age()
    {
        var context = CreateContext();

        var sut = new StrictTransportSecurityGlobalMiddleware((_, _, _) => Task.CompletedTask,
            CreateConfig(86400));

        await sut.Handle(new TransactionStub(true), null, context);

        Assert.That(context.Response.Headers["Strict-Transport-Security"]![0], Is.EqualTo("max-age=86400"));
    }

    [Test]
    public async Task Header_includes_includeSubDomains_when_configured()
    {
        var context = CreateContext();

        var sut = new StrictTransportSecurityGlobalMiddleware((_, _, _) => Task.CompletedTask,
            CreateConfig(includeSubDomains: true));

        await sut.Handle(new TransactionStub(true), null, context);

        Assert.That(context.Response.Headers["Strict-Transport-Security"]![0], Does.Contain("; includeSubDomains"));
    }

    [Test]
    public async Task Header_includes_preload_when_configured()
    {
        var context = CreateContext();

        var sut = new StrictTransportSecurityGlobalMiddleware((_, _, _) => Task.CompletedTask,
            CreateConfig(includeSubDomains: true, preload: true));

        await sut.Handle(new TransactionStub(true), null, context);

        Assert.That(context.Response.Headers["Strict-Transport-Security"]![0], Does.Contain("; preload"));
    }

    [Test]
    public async Task Header_includes_all_directives_when_all_configured()
    {
        var context = CreateContext();

        var sut = new StrictTransportSecurityGlobalMiddleware((_, _, _) => Task.CompletedTask,
            CreateConfig(31536000, true, true));

        await sut.Handle(new TransactionStub(true), null, context);

        Assert.That(context.Response.Headers["Strict-Transport-Security"]![0],
            Is.EqualTo("max-age=31536000; includeSubDomains; preload"));
    }

    [Test]
    public void Constructor_throws_when_preload_without_includeSubDomains()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new StrictTransportSecurityGlobalMiddleware((_, _, _) => Task.CompletedTask,
                new StrictTransportSecurityConfig {
                    Preload = true,
                    IncludeSubDomains = false,
                }));
    }

    [Test]
    public void Constructor_throws_when_preload_with_low_maxAge()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new StrictTransportSecurityGlobalMiddleware((_, _, _) => Task.CompletedTask,
                new StrictTransportSecurityConfig {
                    Preload = true,
                    IncludeSubDomains = true,
                    MaxAge = 100,
                }));
    }

    private static StrictTransportSecurityConfig CreateConfig(int maxAge = 31536000, bool includeSubDomains = false, bool preload = false)
        => new() {
            MaxAge = maxAge,
            IncludeSubDomains = includeSubDomains,
            Preload = preload,
        };

    private static Context CreateContext()
    {
        return new ContextFactory().Create(
            1, Method.Get, "/", [], null, null, "localhost", false, null)!;
    }

    private sealed class TransactionStub(bool secure = false) : ITransaction
    {
        public int Id => 0;
        public Protocol Protocol => Protocol.Http11;
        public bool IsSecure => secure;
        public int Port => secure ? 443 : 80;
        public IPAddress? RemoteAddress => null;
        public Task<RequestDto?> GetRequest(CancellationToken ct = default) => Task.FromResult<RequestDto?>(null);
        public Task SetResponse(ResponseDto response, CancellationToken ct = default) => Task.CompletedTask;
    }
}
