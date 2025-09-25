using Arbiter.Middleware;
using Arbiter.Workers;

namespace Arbiter.Models;

internal class Site
{
    public required string Path { get; set; }
    public List<Uri> Bindings { get; } = [];
    public List<string> DefaultFiles { get; } = [];

    public List<IMiddleware> Middlewares { get; } = [];
    public List<IWorker> Workers { get; } = [];
}