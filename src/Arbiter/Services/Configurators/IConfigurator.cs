using Arbiter.Models.Config;

namespace Arbiter.Services.Configurators;

internal interface IConfigurator
{
    Task Configure(ServerConfigModel serverConfig);
}