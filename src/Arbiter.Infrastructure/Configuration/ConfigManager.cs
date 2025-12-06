using Arbiter.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Arbiter.Infrastructure.Configuration;

internal class ConfigManager : IConfigManager
{
    private const string DataPath = "./data/";

    private readonly IConfiguration _configuration;
    private readonly IEnumerable<IConfigurator> _configurators;
    private readonly SemaphoreSlim _sem = new(1);

    public ConfigManager(IConfiguration configuration, IEnumerable<IConfigurator> configurators)
    {
        _configuration = configuration;
        _configurators = configurators;

        ChangeToken.OnChange(configuration.GetReloadToken, () => Task.Run(async () => await Configure(configuration)));
    }

    public IConfigurationSection? GetDefaultMiddlewareConfig(string name)
    {
        return _configuration
            .GetSection("Default")?
            .Get<DefaultConfig>()?.Middleware?
            .GetValueOrDefault(name)?.Config;
    }

    public async Task CreateDirectories()
    {
        await Task.Run(() => { Directory.CreateDirectory(DataPath); });
    }

    public async Task InitialConfigure()
    {
        await Configure(_configuration);
    }

    public string GetFilePath(string file)
    {
        return Path.Join(DataPath, file);
    }

    private async Task Configure(IConfiguration config)
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
}