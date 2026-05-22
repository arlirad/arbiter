using System.Collections.Concurrent;
using Certify.ACME.Anvil.Acme;

namespace Arbiter.Infrastructure.Acme.Models;

public class DataModel
{
    public ConcurrentBag<IChallengeContext> Challenges
    {
        get;
    } = [];
}
