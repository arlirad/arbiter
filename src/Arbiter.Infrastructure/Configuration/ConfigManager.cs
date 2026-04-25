using Arbiter.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Infrastructure.Configuration;

internal class ConfigManager(IConfiguration configuration) : IConfigManager
{
    private const string DataPath = "./data/";

    private readonly IConfiguration _configuration = configuration;

    public IConfigurationSection? GetDefaultMiddlewareConfig(string name)
    {
        return _configuration
            .GetSection("Default")?
            .Get<DefaultConfig>()?.Middleware?
            .GetValueOrDefault(name)?.Config;
    }

    public IConfigurationSection? GetDefaultWorkerConfig(string name)
    {
        return _configuration
            .GetSection("Default")?
            .Get<DefaultConfig>()?.Workers?
            .GetValueOrDefault(name)?.Config;
    }

    public async Task CreateDirectories() => await Task.Run(() => Directory.CreateDirectory(DataPath));

    public string GetFilePath(string file) => Path.Join(DataPath, file);
}