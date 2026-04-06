using Arbiter.Core.Aggregates;
using Arbiter.Core.Enums;
using Arbiter.Core.Interfaces;
using Arbiter.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Arbiter.Infrastructure.Middleware;

internal class StaticMiddleware(HandleDelegate next) : IMiddleware
{
    private readonly StringComparison _stringComparison = Environment.OSVersion.Platform == PlatformID.Unix
        ? StringComparison.Ordinal
        : StringComparison.OrdinalIgnoreCase;

    private List<string> _defaultFiles = [];
    private Dictionary<string, string> _mimeTypes = new();
    private string _root = null!;

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
            var strippedPath = context.Request.Path.TrimStart('/').Split('?').First();
            var fullPath = Path.GetFullPath(Path.Combine(_root, strippedPath));

            if (!fullPath.StartsWith(_root, _stringComparison))
            {
                await context.Response.Set(Status.NotFound, Stream.Null);
                return;
            }

            var (path, stream) = GetFile(fullPath);

            if (stream is null)
            {
                await context.Response.Set(Status.NotFound, Stream.Null);
                return;
            }

            if (_mimeTypes.TryGetValue(Path.GetExtension(path), out var mime))
                context.Response.Headers.ContentType = mime;

            await context.Response.Set(Status.Ok, stream);
        }
        catch (UnauthorizedAccessException)
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