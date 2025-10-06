using Arbiter.Models.Config;
using Arbiter.Models.Config.Sites;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Services.Configurators;

internal class SiteManagerConfigurator(SiteManager siteManager) : IConfigurator
{
    public async Task Configure(IConfiguration serverConfig)
    {
        try
        {
            var sites = serverConfig
                .GetSection("Sites")
                .Get<Dictionary<string, SiteConfigModel>>();

            if (sites is null)
                return;

            await siteManager.Update(sites);
        }
        catch (Exception e)
        {
            Log.Error("Failed to reload config: {Exception}", e);
        }
    }
}