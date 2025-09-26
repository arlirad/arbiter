using Arbiter.Models;
using Arbiter.Models.Config;
using Arbiter.Models.Network;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Arbiter.Middleware.Static;

internal class StaticMiddleware(IOptionsMonitor<ConfigModel> optionsMonitor) : IMiddleware
{
    private List<string> _defaultFiles = new();
    private Dictionary<string, string> _mimeTypes = new();

    public Task Configure(Site site, IConfigurationSection config)
    {
        var typedConfig = config.Get<StaticMiddlewareConfigModel>();
        
        optionsMonitor.OnChange(ReloadMime);

        _defaultFiles = typedConfig?.DefaultFiles ?? [];
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
            var queryPath = Path.Combine(request.Site.Path, request.Uri.PathAndQuery.TrimStart('/'));
            var (path, stream) = GetFile(queryPath);
            
            if (stream is null)
            {
                await response.Send(HttpStatusCode.NotFound, Stream.Null);
                return;
            }

            if (_mimeTypes.TryGetValue(Path.GetExtension(request.Uri.PathAndQuery), out var mime))
                response.Headers["Content-Type"] = mime;

            await response.Send(HttpStatusCode.OK, stream);
        }
        catch (UnauthorizedAccessException e)
        {
            await response.Send(HttpStatusCode.InternalServerError, Stream.Null);
        }
    }

    private (string path, FileStream? stream) GetFile(string queryPath)
    {
        var stream = TryOpenRead(queryPath);
        if (stream is not null)
            return (queryPath, stream);
        
        foreach (var defaultFile in _defaultFiles)
        {
            var fallbackPath = Path.Combine(queryPath, defaultFile);
            var fallbackStream = TryOpenRead(fallbackPath);
            
            if (fallbackStream is not null)
                return (queryPath, fallbackStream);
        }

        return (queryPath, null);
    }

    private static FileStream? TryOpenRead(string queryPath)
    {
        try
        {
            return (File.GetAttributes(queryPath) & FileAttributes.Directory) == 0 
                ? File.OpenRead(queryPath) 
                : null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}