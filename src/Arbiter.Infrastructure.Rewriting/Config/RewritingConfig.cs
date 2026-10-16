using Arbiter.Infrastructure.Rewriting.Models;

namespace Arbiter.Infrastructure.Rewriting.Config;

public sealed record RewritingConfig
{
    public List<RewritingRule> Rules { get; init; } = null!;
}
