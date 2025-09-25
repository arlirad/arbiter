namespace Arbiter.Models.Config.Sites;

internal class SiteConfigModel
{
    public string? Path { get; set; }
    public List<Uri>? Bindings { get; set; }
    public List<string>? DefaultFiles { get; set; }
    public List<string>? Handlers { get; set; }
    public List<SiteHandlerConfigModel>? Configuration { get; set; }
}