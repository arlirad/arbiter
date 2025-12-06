namespace Arbiter.Application.Configuration;

public class SiteConfig
{
    public string? Path { get; set; }
    public List<Uri>? Bindings { get; set; }
    public List<string>? DefaultFiles { get; set; }
    public List<string>? Handlers { get; set; }

    public List<SiteComponentConfig>? Middleware { get; set; }
    public List<SiteComponentConfig>? Workers { get; set; }
}