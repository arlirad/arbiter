using System.Text.RegularExpressions;

namespace Arbiter.Infrastructure.Rewriting;

public class CompiledRule
{
    private readonly List<Regex> _from = [];
    private string _to = null!;

    private CompiledRule() { }

    public static CompiledRule? Compile(List<string>? from, string? to)
    {
        if (from is null || to is null)
            return null;

        var newRule = new CompiledRule();

        foreach (var line in from)
        {
            var regex = new Regex(line, RegexOptions.Compiled);
            newRule._from.Add(regex);
        }

        newRule._to = to;

        return newRule;
    }

    public string Apply(string input)
    {
        foreach (var line in _from)
        {
            input = line.Replace(input, _to);
        }

        return input;
    }
}