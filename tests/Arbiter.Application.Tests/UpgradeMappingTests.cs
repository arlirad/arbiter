using Arbiter.Application.DTOs;
using Arbiter.Application.Mappers;
using Arbiter.Core.Enums;
using Arbiter.Core.Factories;
using Arbiter.Core.Interfaces;
using Arbiter.Core.ValueObjects;

namespace Arbiter.Application.Tests;

public class UpgradeMappingTests
{
    [Test]
    public void ToDomain_preserves_non_websocket_upgrade()
    {
        var mapper = new ContextMapper(new ContextFactory());
        var upgrade = new FakeUpgrade();
        var request = new RequestDto {
            TransactionId = 42,
            Method = Method.Get,
            Path = "/upgrade",
            Headers = new ReadOnlyHeaders([]),
            Upgrade = upgrade,
        };

        var context = mapper.ToDomain(request);

        Assert.That(context, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(context!.Request.IsUpgrade, Is.True);
            Assert.That(context.Request.Upgrade, Is.SameAs(upgrade));
        }

        Assert.That(context.Request.Upgrade, Is.Not.AssignableTo<IWebSocketUpgrade>());
    }

    private sealed class FakeUpgrade : IUpgrade
    {
        public Task<Stream> AcceptAsync(ReadOnlyHeaders? responseHeaders = null) => throw new NotSupportedException();
    }
}
