using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Interfaces;
using Arbiter.Infrastructure.Rewriting.Models;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Infrastructure.Rewriting;

public class RewritingMiddleware(HandleDelegate next) : IMiddleware
{
    private List<CompiledRule> _compiledRules = [];

    public Task Configure(Site site, IConfiguration config)
    {
        var typedConfig = config.Get<ConfigModel>();

        if (typedConfig?.Rules is null)
            throw new Exception("rules are not set");

        _compiledRules = typedConfig.Rules.Select(r => CompiledRule.Compile(r.From, r.To))
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

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