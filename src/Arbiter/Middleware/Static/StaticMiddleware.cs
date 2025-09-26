using Arbiter.Models;
using Arbiter.Models.Config;
using Arbiter.Models.Network;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Arbiter.Middleware.Static;

internal class StaticMiddleware : IMiddleware
{
    private List<string> _defaultFiles = [];
    private Dictionary<string, string> _mimeTypes = new();

    public Task Configure(Site site, IConfiguration config)
    {
        var typedConfig = config.Get<StaticMiddlewareConfigModel>();
        
        _defaultFiles = typedConfig?.DefaultFiles ?? [];
        _mimeTypes = typedConfig?.Mime ?? [];
        
        return Task.CompletedTask;
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

            if (_mimeTypes.TryGetValue(Path.GetExtension(path), out var mime))
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
                return (fallbackPath, fallbackStream);
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
        catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
        {
            return null;
        }
    }
}