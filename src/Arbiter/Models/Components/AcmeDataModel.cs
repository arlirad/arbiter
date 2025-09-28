using Certify.ACME.Anvil.Acme;

namespace Arbiter.Models.Components;

public class AcmeDataModel
{
    public List<IChallengeContext> Challenges { get; } = [];
}