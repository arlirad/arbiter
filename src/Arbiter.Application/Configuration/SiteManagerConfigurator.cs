using Arbiter.Application.Interfaces;
using Arbiter.Application.Managers;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Arbiter.Application.Configuration;

internal class SiteManagerConfigurator(SiteManager siteManager) : IConfigurator
{
    public async Task Configure(IConfiguration serverConfig)
    {
        try
        {
            var sites = serverConfig
                .GetSection("Sites")
                .Get<Dictionary<string, SiteConfig>>();

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