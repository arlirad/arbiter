using Arbiter.Models.Config;
using Serilog;

namespace Arbiter.Services.Configurators;

internal class SiteManagerConfigurator(SiteManager siteManager) : IConfigurator
{
    public async Task Configure(ServerConfigModel serverConfig)
    {
        try
        {
            await siteManager.Update(serverConfig);
        }
        catch (Exception e)
        {
            Log.Error("Failed to reload config: {Exception}", e);
        }
    }
}