namespace Arbiter.Models.Config.Sites;

public class SiteConfigModel
{
    public string? Path { get; set; }
    public List<Uri>? Bindings { get; set; }
    public List<string>? DefaultFiles { get; set; }
    public List<string>? Handlers { get; set; }

    public List<SiteComponentConfigModel>? Middleware { get; set; }
    public List<SiteComponentConfigModel>? Workers { get; set; }
}