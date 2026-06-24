using Arbiter.Application.Interfaces;
using Arbiter.Core.Aggregates;
using Arbiter.Core.Interfaces;
using Arbiter.Infrastructure.Rewriting.Config;

namespace Arbiter.Infrastructure.Rewriting;

public class RewritingMiddleware(HandleDelegate next) : IConfigurableMiddleware<RewritingConfig>
{
    private List<CompiledRule> _compiledRules = [];

    public Task Configure(ComponentDataContainer data, RewritingConfig config)
    {
        if (config.Rules is null)
            throw new Exception("rules are not set");

        _compiledRules = [
            .. config.Rules.Select(r => CompiledRule.Compile(r.From, r.To))
                .Where(r => r is not null)
                .Select(r => r!),
        ];

        return Task.CompletedTask;
    }

    public Task Handle(Context context)
    {
        var path = context.Request.Path;

        foreach (var rule in _compiledRules)
        {
            var newPath = rule.Apply(path);

            if (newPath == path)
                continue;

            path = newPath;

            break;
        }

        context.Request.Path = path;

        return next(context);
    }
}
