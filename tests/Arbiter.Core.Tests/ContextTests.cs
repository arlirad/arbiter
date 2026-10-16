using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Core.Tests;

public class ContextTests
{
    [Test]
    public async Task AcceptUpgradeAsync_returns_stream_and_sets_IsUpgraded()
    {
        var upgrade = new FakeUpgrade();
        var context = CreateContext(upgrade);

        var stream = await context.AcceptUpgradeAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stream, Is.SameAs(upgrade.UpgradedStream));
            Assert.That(context.IsUpgraded, Is.True);
        }
    }

    [Test]
    public async Task AcceptUpgradeAsync_forwards_response_headers()
    {
        var upgrade = new FakeUpgrade();
        var context = CreateContext(upgrade);

        var headers = new Headers();
        headers["X"] = ["a"];
        var responseHeaders = new ReadOnlyHeaders(headers);

        await context.AcceptUpgradeAsync(responseHeaders);

        Assert.That(upgrade.ReceivedHeaders, Is.SameAs(responseHeaders));
    }

    [Test]
    public async Task AcceptUpgradeAsync_passes_null_headers_by_default()
    {
        var upgrade = new FakeUpgrade();
        var context = CreateContext(upgrade);

        await context.AcceptUpgradeAsync();

        Assert.That(upgrade.ReceivedHeaders, Is.Null);
    }

    [Test]
    public void AcceptUpgradeAsync_throws_when_request_is_not_upgrade()
    {
        var context = CreateContext(null);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await context.AcceptUpgradeAsync());
        Assert.That(context.IsUpgraded, Is.False);
    }

    [Test]
    public void IsUpgraded_is_false_initially()
    {
        var context = CreateContext(new FakeUpgrade());

        Assert.That(context.IsUpgraded, Is.False);
    }

    private static Context CreateContext(IUpgrade? upgrade)
        => new ContextFactory().Create(1, Method.Get, "/", [], null, upgrade, null, false, null)!;

    private sealed class FakeUpgrade : IUpgrade
    {
        public MemoryStream UpgradedStream { get; } = new();

        public ReadOnlyHeaders? ReceivedHeaders
        {
            get;
            private set;
        }

        public Task<Stream> AcceptAsync(ReadOnlyHeaders? responseHeaders = null)
        {
            ReceivedHeaders = responseHeaders;

            return Task.FromResult<Stream>(UpgradedStream);
        }
    }
}
