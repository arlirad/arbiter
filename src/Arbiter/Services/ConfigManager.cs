using Arbiter.Models.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Arbiter.Services;

internal class ConfigManager(IOptionsMonitor<ServerConfigModel> configMonitor)
{
    public string DataPath { get; } = "./data/";

    public IConfigurationSection? GetDefaultMiddlewareConfig(string name)
    {
        return configMonitor.CurrentValue.Default?.Middleware?.GetValueOrDefault(name)?.Config;
    }

    public async Task CreateDirectories()
    {
        await Task.Run(() => { Directory.CreateDirectory(DataPath); });
    }
}