using Arbiter.Models.Config;
using Arbiter.Services.Configurators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Arbiter.Services;

internal class ConfigManager
{
    private readonly List<Func<ServerConfigModel, Task>> _callbacks = [];

    private readonly IOptionsMonitor<ServerConfigModel> _configMonitor;
    private readonly IEnumerable<IConfigurator> _configurators;
    private readonly SemaphoreSlim _sem = new(1);

    public ConfigManager(IOptionsMonitor<ServerConfigModel> configMonitor, IEnumerable<IConfigurator> configurators)
    {
        _configMonitor = configMonitor;
        _configurators = configurators;

        _configMonitor.OnChange(ReloadConfig);
    }

    public string DataPath { get; } = "./data/";

    public IConfigurationSection? GetDefaultMiddlewareConfig(string name)
    {
        return _configMonitor.CurrentValue.Default?.Middleware?.GetValueOrDefault(name)?.Config;
    }

    public async Task CreateDirectories()
    {
        await Task.Run(() => { Directory.CreateDirectory(DataPath); });
    }

    public async Task InitialConfigure()
    {
        await Configure(_configMonitor.CurrentValue);
    }

    private async Task Configure(ServerConfigModel config)
    {
        await _sem.WaitAsync();

        try
        {
            await Task.WhenAll(_configurators.Select(c => c.Configure(config)));
        }
        finally
        {
            _sem.Release();
        }
    }

    private void ReloadConfig(ServerConfigModel config, string? ignored)
    {
        _ = Configure(config);
    }
}