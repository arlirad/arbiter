using Arbiter.Models.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Arbiter.Services;

internal class ConfigManager(IOptionsMonitor<ServerConfigModel> configMonitor)
{
    public IConfigurationSection? GetDefaultMiddlewareConfig(string name)
    {
        return configMonitor.CurrentValue.Default?.Middleware?.GetValueOrDefault(name)?.Config;
    } 
}