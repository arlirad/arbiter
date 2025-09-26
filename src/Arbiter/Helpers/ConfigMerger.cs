using Microsoft.Extensions.Configuration;

namespace Arbiter.Helpers;

public static class ConfigMerger
{
    public static IConfiguration Merge(params IConfiguration?[] configs)
    {
        var builder = new ConfigurationBuilder();
        
        foreach (var c in configs)
        {
            if (c is not null)
                builder.AddConfiguration(c);
        }
        
        return builder.Build()!;
    }
}