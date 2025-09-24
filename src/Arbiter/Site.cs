using Arbiter.Handlers;

namespace Arbiter;

public class Site
{
    public string Path { get; set; } = null;
    public List<Uri> Bindings { get; set; } = new();
    public List<string> Rewriters { get; set; } = new();
    public List<string> DefaultDocs { get; set; } = new();
    public Dictionary<string, string> Parameters { get; set; } = new();
    public List<IHandler> Handlers { get; } = new();
}