using Microsoft.Extensions.Configuration;

namespace Arbiter.Application.Interfaces;

public interface IConfigManager
{
    IConfigurationSection? GetDefaultMiddlewareConfig(string name);
    IConfigurationSection? GetDefaultWorkerConfig(string name);
    Task CreateDirectories();
    Task InitialConfigure();
    string GetFilePath(string file);
}