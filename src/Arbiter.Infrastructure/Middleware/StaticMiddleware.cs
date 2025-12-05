using Arbiter.Domain.Aggregates;
using Arbiter.Domain.Enums;
using Arbiter.Domain.Interfaces;
using Arbiter.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Infrastructure.Middleware;

internal class StaticMiddleware(HandleDelegate next) : IMiddleware
{
    private List<string> _defaultFiles = [];
    private Dictionary<string, string> _mimeTypes = new();
    private string _root;

    public Task Configure(Site site, IConfiguration config)
    {
        var typedConfig = config.Get<StaticMiddlewareConfig>();

        _defaultFiles = typedConfig?.DefaultFiles ?? [];
        _mimeTypes = typedConfig?.Mime ?? [];
        _root = site.Path;

        return Task.CompletedTask;
    }

    public async Task Handle(Context context)
    {
        try
        {
            var queryPath = Path.Combine(_root, context.Request.Path.TrimStart('/'));
            var (path, stream) = GetFile(queryPath);

            if (stream is null)
            {
                await context.Response.Set(Status.NotFound, Stream.Null);
                return;
            }

            if (_mimeTypes.TryGetValue(Path.GetExtension(path), out var mime))
                context.Response.Headers.ContentType = mime;

            await context.Response.Set(Status.Ok, stream);
        }
        catch (UnauthorizedAccessException e)
        {
            await context.Response.Set(Status.InternalServerError, Stream.Null);
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