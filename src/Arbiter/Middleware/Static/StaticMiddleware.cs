using Arbiter.Models;
using Arbiter.Models.Config;
using Arbiter.Models.Network;
using Microsoft.Extensions.Options;

namespace Arbiter.Middleware.Static;

internal class StaticMiddleware(IOptionsMonitor<ConfigModel> optionsMonitor) : IMiddleware
{
    private Dictionary<string, string> _mimeTypes = new();

    public Task Configure(Site site, object config)
    {
        optionsMonitor.OnChange(ReloadMime);
        ReloadMime(optionsMonitor.CurrentValue, null);
        
        return Task.CompletedTask;
    }

    private void ReloadMime(ConfigModel config, string? _)
    {
        if (config.Mime is not null)
            _mimeTypes = new Dictionary<string, string>(config.Mime);
    }

    public Task<bool> CanHandle(HttpRequestContext request)
    {
        return Task.FromResult(true);
    }

    public async Task Handle(HttpRequestContext request, HttpResponseContext response)
    {
        try
        {
            var path = Path.Combine(request.Site.Path, request.Uri.PathAndQuery.TrimStart('/'));
            var stream = File.OpenRead(path);

            if (_mimeTypes.TryGetValue(Path.GetExtension(request.Uri.PathAndQuery), out var mime))
                response.Headers["Content-Type"] = mime;

            await response.Send(HttpStatusCode.Found, stream);
        }
        catch (FileNotFoundException e)
        {
            await response.Send(HttpStatusCode.NotFound, Stream.Null);
        }
    }
}