using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Interfaces;

public interface IConfigManager
{
    IConfigurationSection? GetDefaultMiddlewareConfig(string name);
    IConfigurationSection? GetDefaultWorkerConfig(string name);
    Task CreateDirectories();
    string GetFilePath(string file);
}
