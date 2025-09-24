using Arbiter.Models.Config.Site;

namespace Arbiter.Models.Config;

internal class SiteConfigModel
{
    public string? Path { get; set; }
    public List<Uri>? Bindings { get; set; }
    public List<string>? DefaultFiles { get; set; }
    public List<string>? Handlers { get; set; }
    public List<HandlerConfigModel>? Configuration { get; set; }
}