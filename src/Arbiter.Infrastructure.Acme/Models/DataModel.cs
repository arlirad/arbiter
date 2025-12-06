using Certify.ACME.Anvil.Acme;

namespace Arbiter.Infrastructure.Acme.Models;

public class DataModel
{
    public List<IChallengeContext> Challenges { get; } = [];
}